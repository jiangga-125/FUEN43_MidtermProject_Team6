// Areas/ReportMail/Controllers/ReportsController.cs
using Microsoft.AspNetCore.Mvc;
using ReportMail.Services.Reports;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;


namespace ReportMail.Areas.ReportMail.Controllers
{
    /// <summary>
    /// 報表主頁 + 三張「預設」不可編輯的報表 API。
    /// 預設圖型與邏輯寫死在服務層（ShopReportDataService）：
    /// 1) 折線圖 Line：近 30 天「總銷售金額」，可切顆粒度 day/month/year
    /// 2) 長條圖 Bar：近 30 天「銷售書籍排行」，預設 Top10
    /// 3) 圓餅圖 Pie：近 30 天「借閱書籍種類」排行，預設 Top5
    ///
    /// ※ 自訂報表（ReportDefinition/ReportFilter）之後另做 CRUD 與對應 API，不混在這支。
    /// </summary>
    [Area("ReportMail")]
    // 讓 URL 穩定為 /ReportMail/Reports/{Action}
    [Route("ReportMail/[controller]/[action]")]
    public class ReportsController : Controller
    {
        private readonly IReportDataService _svc;
        private readonly ReportMailDbContext _db;

        public ReportsController(IReportDataService svc, ReportMailDbContext db)
        {
            _svc = svc;
            _db = db;
        }

        /// <summary>
        /// 主頁（一次顯示三張預設圖）。
        /// View：Areas/ReportMail/Views/Reports/Index.cshtml
        /// </summary>
        [HttpGet]
        // 也讓 /ReportMail/Reports 直接進到這頁（不用寫 /Index）
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
        /// 預設：近 30 天（含今日）、顆粒度 granularity = day。
        /// 可選 granularity：day / month / year
        /// 可選 excludeStatuses：?excludeStatuses=0&excludeStatuses=9（例如排除取消/作廢）
        /// </summary>
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Line(
            DateTime? from,              // 起日（yyyy-MM-dd），未給則 = 今天往前 29 天
            DateTime? to,                // 迄日（yyyy-MM-dd），未給則 = 今天
            string granularity = "day",  // day / month / year
            [FromQuery] int[]? excludeStatuses = null)
        {
            NormalizeDateRange(from, to, out var start, out var end);

            granularity = (granularity ?? "day").Trim().ToLowerInvariant();
            if (granularity is not ("day" or "month" or "year"))
                return BadRequest("granularity 僅允許 day / month / year");

            var points = await _svc.GetSalesAmountSeriesAsync(start, end, granularity, excludeStatuses);
            return Ok(new
            {
                labels = points.Select(p => p.Label).ToArray(),
                data = points.Select(p => p.Value).ToArray()
            });
        }

        /// <summary>
        /// 長條圖：銷售書籍排行（以銷售數量排序）。
        /// 預設：近 30 天 Top10。
        /// 可選 excludeStatuses：?excludeStatuses=0&excludeStatuses=9
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

            var points = await _svc.GetTopSoldBooksAsync(start, end, top, excludeStatuses);
            return Ok(new
            {
                labels = points.Select(p => p.Label).ToArray(),
                data = points.Select(p => p.Value).ToArray()
            });
        }

        /// <summary>
        /// 圓餅圖：借閱書籍「種類」排行（以借閱筆數計）。
        /// 預設：近 30 天 Top5。
        /// ※ 若你的 BorrowRecords 走 Listings 流程，對應 Join 已在服務層處理
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

            var points = await _svc.GetTopBorrowCategoryAsync(start, end, top);
            return Ok(new
            {
                labels = points.Select(p => p.Label).ToArray(),
                data = points.Select(p => p.Value).ToArray()
            });
        }

        #region helpers

        /// <summary>
        /// 正常化日期區間：
        /// - 沒給參數：預設近 30 天（含今天），即 [end-29, end]
        /// - 只給其中一端：另一端補齊
        /// - 確保 start <= end；回傳的 start/end 都是 Date（時間 = 00:00）
        /// </summary>
        private static void NormalizeDateRange(DateTime? from, DateTime? to, out DateTime start, out DateTime end)
        {
            end = (to ?? DateTime.Today).Date;       // 今天
            start = (from ?? end.AddDays(-29)).Date;     // 近 30 天
            if (start > end) (start, end) = (end, start); // 交換，防呆
        }

        #endregion
    }
}
