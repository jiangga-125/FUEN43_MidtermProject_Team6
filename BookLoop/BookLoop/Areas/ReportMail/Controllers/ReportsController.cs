// Areas/ReportMail/Controllers/ReportsController.cs
using BookLoop.Models;
using BookLoop.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


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

		public ReportsController(IReportDataService svc, ReportMailDbContext db)
		{
			_svc = svc;
			_db = db;
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
		// 也讓 /ReportMail/Reports 直接進到這頁（不用寫 /Index）
		[Route("/ReportMail/Reports")]
		public async Task<IActionResult> Index()
		{
			var activeDefinitions = await _db.ReportDefinitions
					.AsNoTracking()
					.Where(r => r.IsActive)
					.ToListAsync();

			static List<ReportDefinition> FilterByCategory(IEnumerable<ReportDefinition> source, string category)
					=> source
							.Where(r => !string.IsNullOrWhiteSpace(r.Category)
									&& string.Equals(r.Category.Trim(), category, StringComparison.OrdinalIgnoreCase))
							.OrderBy(r => r.ReportName)
							.ToList();

			ViewBag.LineReports = FilterByCategory(activeDefinitions, "line");
			ViewBag.BarReports = FilterByCategory(activeDefinitions, "bar");
			ViewBag.PieReports = FilterByCategory(activeDefinitions, "pie");

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

			var points = await _svc.GetSalesAmountSeriesAsync(start, end, granularity, excludeStatuses);

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

			var points = await _svc.GetTopSoldBooksAsync(start, end, top, excludeStatuses);
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

			var points = await _svc.GetTopBorrowBooksAsync(start, end, top);
			return Ok(new
			{
				title = $"總借閱次數",
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
