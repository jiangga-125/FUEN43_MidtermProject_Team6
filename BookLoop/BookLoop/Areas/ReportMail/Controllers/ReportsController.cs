// Areas/ReportMail/Controllers/ReportsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Contexts;
using BookLoop.Services.Reports;

namespace ReportMail.Areas.ReportMail.Controllers
{
    /// <summary>
    /// 報表主頁 + 三張「預設」不可編輯的報表 API。
    /// 1) 折線圖 Line：近 30 天「總銷售金額」，可切顆粒度 day/month/year
    /// 2) 長條圖 Bar：近 30 天「銷售書籍排行」（本數），預設 Top10
    /// 3) 圓餅圖 Pie：近 30 天「借閱書籍種類」（筆數），預設 Top5
    /// </summary>
    [Area("ReportMail")]
    [Authorize(Roles = "Admin,Marketing,Publisher")] // 一般員工無法進入整個頁面/API
    [Route("ReportMail/[controller]/[action]")]
    public class ReportsController : Controller
    {
        private readonly IReportDataService _svc;
        private readonly ReportMailDbContext _db;
        private readonly IPublisherScopeService _scope;

        public ReportsController(IReportDataService svc, ReportMailDbContext db, IPublisherScopeService scope)
        {
            _svc = svc;
            _db = db;
            _scope = scope;
        }

        /// <summary>
        /// 主頁（一次顯示三張預設圖）。
        /// View：Areas/ReportMail/Views/Reports/Index.cshtml
        /// </summary>
        [HttpGet]
        [Route("/ReportMail/Reports")]
        public async Task<IActionResult> Index()
        {
            var defs = _db.ReportDefinitions
                          .AsNoTracking()
                          .Where(r => r.IsActive);

            ViewBag.LineReports = await defs
                .Where(r => r.Category != null && r.Category.ToLower() == "line")
                .OrderBy(r => r.ReportName)
                .ToListAsync();

            ViewBag.BarReports = await defs
                .Where(r => r.Category != null && r.Category.ToLower() == "bar")
                .OrderBy(r => r.ReportName)
                .ToListAsync();

            ViewBag.PieReports = await defs
                .Where(r => r.Category != null && r.Category.ToLower() == "pie")
                .OrderBy(r => r.ReportName)
                .ToListAsync();

            return View();
        }

        /// <summary>
        /// 折線圖：總銷售金額序列。
        /// 預設：近 30 天（含今日）、granularity=day。
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Line(
            DateTime? from,
            DateTime? to,
            string granularity = "day",
            [FromQuery] int[]? excludeStatuses = null)
        {
            NormalizeDateRange(from, to, out var start, out var end);

            granularity = (granularity ?? "day").Trim().ToLowerInvariant();
            if (granularity is not ("day" or "month" or "year"))
                return BadRequest("granularity 僅允許 day / month / year");

            var publisherIds = GetEffectivePublisherIdsOrNull();

            var points = await _svc.GetSalesAmountSeriesAsync(
                start, end, granularity, excludeStatuses, publisherIds);

            if (granularity == "day")
            {
                var map = points.ToDictionary(p => p.Label, p => p.Value, StringComparer.Ordinal);
                var labels = new System.Collections.Generic.List<string>((end - start).Days + 1);
                var data   = new System.Collections.Generic.List<decimal>((end - start).Days + 1);

                for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                {
                    var lab = d.ToString("yyyy-MM-dd");
                    labels.Add(lab);
                    data.Add(map.TryGetValue(lab, out var v) ? v : 0m);
                }

                return Ok(new { title = "每日銷售金額", labels, data });
            }

            return Ok(new
            {
                labels = points.Select(p => p.Label).ToArray(),
                data   = points.Select(p => p.Value).ToArray()
            });
        }

        /// <summary>
        /// 長條圖：銷售書籍排行（以銷售數量排序）。預設：近 30 天 Top10。
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Bar(
            DateTime? from,
            DateTime? to,
            int top = 10,
            [FromQuery] int[]? excludeStatuses = null)
        {
            NormalizeDateRange(from, to, out var start, out var end);
            if (top <= 0) top = 10;

            var publisherIds = GetEffectivePublisherIdsOrNull();

            var points = await _svc.GetTopSoldBooksAsync(
                start, end, top, excludeStatuses, publisherIds);

            return Ok(new
            {
                title  = "總銷售本數",
                labels = points.Select(p => p.Label).ToArray(),
                data   = points.Select(p => p.Value).ToArray()
            });
        }

        /// <summary>
        /// 圓餅圖：借閱書籍「種類」排行（以借閱筆數計）。預設：近 30 天 Top5。
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Pie(
            DateTime? from,
            DateTime? to,
            int top = 5)
        {
            NormalizeDateRange(from, to, out var start, out var end);
            if (top <= 0) top = 5;

            var publisherIds = GetEffectivePublisherIdsOrNull();

            var points = await _svc.GetTopBorrowCategoryAsync(
                start, end, top, publisherIds);

            return Ok(new
            {
                title  = "總借閱本數",
                labels = points.Select(p => p.Label).ToArray(),
                data   = points.Select(p => p.Value).ToArray()
            });
        }

        #region helpers

        /// <summary>
        /// Admin/Marketing：不限制（回傳 null）
        /// Publisher（書商）：回傳可視 PublisherIDs（空陣列代表無資料）
        /// 其他：保守回空陣列（Authorize 已擋，但雙重保護）
        /// </summary>
        private int[]? GetEffectivePublisherIdsOrNull()
        {
            if (_scope.IsAdminOrMarketing(User)) return null;
            if (_scope.IsPublisher(User))
            {
                var ids = _scope.GetPublisherIds(User) ?? Array.Empty<int>();
                return ids.Length == 0 ? Array.Empty<int>() : ids;
            }
            return Array.Empty<int>();
        }

        /// <summary>
        /// 正常化日期區間：
        /// - 沒給參數：預設近 30 天（含今天），即 [end-29, end]
        /// - 只給其中一端：另一端補齊
        /// - 確保 start <= end；回傳的 start/end 都是 Date（時間 = 00:00）
        /// </summary>
        private static void NormalizeDateRange(DateTime? from, DateTime? to, out DateTime start, out DateTime end)
        {
            end = (to ?? DateTime.Today).Date;
            start = (from ?? end.AddDays(-29)).Date;
            if (start > end) (start, end) = (end, start);
        }

        #endregion
    }
}
