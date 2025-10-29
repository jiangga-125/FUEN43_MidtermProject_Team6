using BookLoop.Models;
using BookLoop.Data;
using BookLoop.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims; // 引用 ClaimTypes


namespace ReportMail.Areas.ReportMail.Controllers
{
    /// <summary>
    /// 報表主頁 + 三張「預設」不可編輯的報表 API。
    /// 預設圖型與邏輯寫死在服務層（ShopReportDataService）：
    /// 1) 折線圖 Line：近 30 天「總銷售金額」，可切顆粒度 day/month/year
    /// 2) 長條圖 Bar：近 30 天「銷售書籍排行」，預設 Top10
    /// 3) 圓餅圖 Pie：近 30 天「借閱書籍種類」排行，預設 Top5
    ///
    /// ※ 自訂報表（ReportDefinition/ReportFilter）另做 CRUD 與對應 API，不混在這支。
    /// </summary>
    [Area("ReportMail")]
    [Authorize(Policy = "ReportMail.Access")]
    // 讓 URL 穩定為 /ReportMail/Reports/{Action}
    [Route("ReportMail/[controller]/[action]")]
    public class ReportsController : Controller
    {
        private readonly IReportDataService _svc;
        private readonly ReportMailDbContext _db;
        private readonly ShopDbContext _shop;
        private readonly IAuthorizationService _authService;


        public ReportsController(IReportDataService svc, ReportMailDbContext db, ShopDbContext shop, IAuthorizationService authService)
        {
            _svc = svc;
            _db = db;
            _shop = shop;
            _authService = authService;
        }


        [HttpGet("/ReportMail/Reports/whoami")]
        [Authorize] // 只要求登入，不套報表 Policy
        public IActionResult WhoAmI()
        {
            var auth = User?.Identity?.IsAuthenticated ?? false;
            var name = User?.Identity?.Name ?? "(anonymous)";
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Json(new { authenticated = auth, name, claims });
        }

        /// <summary>
        /// 主頁（一次顯示三張預設圖）。
        /// View：Areas/ReportMail/Views/Reports/Index.cshtml
        /// </summary>
        [HttpGet]
        [Route("/ReportMail/Reports")]
        public async Task<IActionResult> Index()
        {
            List<ReportDefinition> accessibleDefinitions;
            var canViewAny = (await _authService.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewAny")).Succeeded;

            if (canViewAny)
            {
                // 報表定義的存取權限 (ViewAny / ViewOwn) 邏輯已在 ReportDefinitionsController.cs 中處理
                accessibleDefinitions = await _db.ReportDefinitions
                    .AsNoTracking()
                    .Where(r => r.IsActive)
                    .ToListAsync();
            }
            else
            {
                var canViewOwn = (await _authService.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewOwn")).Succeeded;
                if (canViewOwn)
                {
                    var myId = CurrentUserIdOrNull(); // 使用輔助方法
                    if (myId is null)
                    {
                        accessibleDefinitions = new List<ReportDefinition>(); // 無法識別使用者，不顯示自訂報表
                    }
                    else
                    {
                        // 一般使用者/書商：只看到自己建立且 IsActive 的報表
                        accessibleDefinitions = await _db.ReportDefinitions
                            .AsNoTracking()
                            .Where(r => r.IsActive && r.OwnerUserID == myId)
                            .OrderBy(r => r.ReportName)
                            .ToListAsync();
                    }
                }
                else
                {
                    // 無 ViewAny 且無 ViewOwn 權限
                    accessibleDefinitions = new List<ReportDefinition>();
                }
            }
            static List<ReportDefinition> FilterByCategory(IEnumerable<ReportDefinition> source, string category)
                    => source
                            .Where(r => !string.IsNullOrWhiteSpace(r.Category)
                                    && string.Equals(r.Category.Trim(), category, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(r => r.ReportName)
                            .ToList();

            ViewBag.LineReports = FilterByCategory(accessibleDefinitions, "line");
            ViewBag.BarReports = FilterByCategory(accessibleDefinitions, "bar");
            ViewBag.PieReports = FilterByCategory(accessibleDefinitions, "pie");

            // 將使用者 Email 和管理員狀態傳遞給 View 
            string? userEmail = User.FindFirstValue(ClaimTypes.Email); // 獲取 Email Claim
            ViewBag.UserEmail = userEmail ?? ""; // 如果找不到 Email Claim，傳遞空字串
            ViewBag.IsAdmin = canViewAny;       // canViewAny 變數代表是否為管理員 (能看所有報表定義)

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
        [Authorize(Policy = "ReportMail.Reports.Query")]
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

            // ★ 修正：現在 ResolveSupplierScopeAsync 回傳的是 SupplierID?
            var supplierId = await ResolveSupplierScopeAsync();

            // ★ 將 supplierId 傳遞給服務層
            var points = await _svc.GetSalesAmountSeriesAsync(start, end, granularity, excludeStatuses, supplierId);

            // 預設（day）→ 補零：確保 30 天每日都有一個點
            if (granularity == "day")
            {
                // 將服務回來的序列轉成 map：label -> value
                // （服務層 day 的 label 預期為 "yyyy-MM-dd"）
                var map = points.ToDictionary(p => p.Label, p => p.Value, StringComparer.Ordinal);

                var labels = new List<string>(capacity: (end - start).Days + 1);
                var data = new List<decimal>(capacity: (end - start).Days + 1);

                for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                {
                    var lab = d.ToString("yyyy-MM-dd");
                    labels.Add(lab);
                    data.Add(map.TryGetValue(lab, out var v) ? v : 0m); // 沒資料 → 補 0
                }

                return Ok(new
                {
                    title = "每日銷售金額",
                    labels,
                    data
                });
            }

            // 非 day（例如 month/year）保持原邏輯
            return Ok(new
            {
                title = "總銷售金額序列", // 補上 title
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
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> Bar(
            DateTime? from,
            DateTime? to,
            int top = 10,
            [FromQuery] int[]? excludeStatuses = null)
        {
            NormalizeDateRange(from, to, out var start, out var end);
            if (top <= 0) top = 10;

            // ★ 修正：現在 ResolveSupplierScopeAsync 回傳的是 SupplierID?
            var supplierId = await ResolveSupplierScopeAsync();
            // ★ 將 supplierId 傳遞給服務層
            var points = await _svc.GetTopSoldBooksAsync(start, end, top, excludeStatuses, supplierId);
            return Ok(new
            {
                title = $"總銷售本數",
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
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> Pie(
            DateTime? from,
            DateTime? to,
            int top = 5)
        {
            NormalizeDateRange(from, to, out var start, out var end);
            if (top <= 0) top = 5;

            // ★ 修正：現在 ResolveSupplierScopeAsync 回傳的是 SupplierID?
            var supplierId = await ResolveSupplierScopeAsync();
            // ★ 將 supplierId 傳遞給服務層
            var points = await _svc.GetTopBorrowBooksAsync(start, end, top, supplierId);
            return Ok(new
            {
                title = $"總借閱次數",
                labels = points.Select(p => p.Label).ToArray(),
                data = points.Select(p => p.Value).ToArray()
            });
        }

#if DEBUG
        [HttpGet("/ReportMail/Reports/diag")]
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> Diag(DateTime? from, DateTime? to, [FromQuery] int[]? excludeStatuses = null)
        {
            // 1) 日期：統一半開區間 [start, endExclusive)
            NormalizeDateRange(from, to, out var start, out var end);
            var startDate = start.Date;
            var endExclusive = end.Date.AddDays(1);

            // 2) 範圍：supplierId（null=ALL；int=指定 Supplier）
            var supplierId = await ResolveSupplierScopeAsync();
            var pubDesc = supplierId is null ? "ALL" : (supplierId == 0 ? "NONE" : supplierId.ToString());

            // 3) 三條 count —— 模擬服務層的篩選邏輯
            // 3a) 銷售（Orders/OrderDetails -> Books -> Publishers）
            var salesQ =
                from od in _shop.OrderDetails.AsNoTracking()
                join o in _shop.Orders.AsNoTracking() on od.OrderID equals o.OrderID
                join b in _shop.Books.AsNoTracking() on od.BookID equals b.BookID
                join p in _shop.Publishers.AsNoTracking() on b.PublisherID equals p.PublisherID
                where o.OrderDate >= startDate && o.OrderDate < endExclusive
                select new { o.Status, p.SupplierID }; // ★ 改成 SupplierID

            if (excludeStatuses is { Length: > 0 })
            {
                var excl = excludeStatuses;
                salesQ = salesQ.Where(x => !excl.Contains((int)x.Status));
            }
            // ★ Data Scope 限制：直接使用 SupplierID
            if (supplierId.HasValue && supplierId.Value > 0)
                salesQ = salesQ.Where(x => x.SupplierID == supplierId);
            else if (supplierId == 0) // 無效 Supplier
                return Ok(new { start = startDate, endExclusive, supplierId = pubDesc, sales = 0, bar = 0, borrow = 0 });

            var sales = await salesQ.CountAsync();

            // 3b) 長條圖 TopN 其實同一條 salesQ 就能代表是否有資料，這裡就不重複寫

            // 3c) 借閱（BorrowRecords -> Listings -> Publishers）
            var borrowQ =
                from br in _shop.BorrowRecords.AsNoTracking()
                join l in _shop.Listings.AsNoTracking() on br.ListingID equals l.ListingID
                join p in _shop.Publishers.AsNoTracking() on l.PublisherID equals p.PublisherID
                where br.BorrowDate >= startDate && br.BorrowDate < endExclusive
                select new { p.SupplierID }; // ★ 改成 SupplierID

            // ★ Data Scope 限制：直接使用 SupplierID
            if (supplierId.HasValue && supplierId.Value > 0)
                borrowQ = borrowQ.Where(x => x.SupplierID == supplierId);
            else if (supplierId == 0)
                return Ok(new { start = startDate, endExclusive, supplierId = pubDesc, sales, borrow = 0 });

            var borrow = await borrowQ.CountAsync();

            return Ok(new
            {
                start = startDate,
                endExclusive,
                supplierId = pubDesc,
                sales,
                borrow
            });
        }
#endif


        //helpers

        private int? CurrentUserIdOrNull()
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s, out var id) ? id : (int?)null;
        }

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

        /// <summary>
        /// 取得資料範圍的 SupplierID
        /// - 有 Data.All 權限 => 回傳 null (全部)
        /// - 有 supplier claim 且有效 => 回傳 int (指定 SupplierID)
        /// - 其他 (無權限/無效 claim) => 回傳 0 (無資料)
        /// </summary>
        private async Task<int?> ResolveSupplierScopeAsync()
        {
            // 有 Data.All => 回傳 null 代表「不加限制」
            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAll = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Data.All")).Succeeded;
            if (canAll) return null;

            // 沒有 Data.All => 依使用者 supplier claim 取 SupplierID
            var supplierIdClaim = User.FindFirst("supplier")?.Value;
            if (!int.TryParse(supplierIdClaim, out var mySupplierId) || mySupplierId <= 0) return 0; // 無效 claim → 視為無資料 (0)

            return mySupplierId;
        }

    }
}