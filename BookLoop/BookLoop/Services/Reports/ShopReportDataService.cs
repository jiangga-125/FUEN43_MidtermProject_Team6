// Services/Reports/ShopReportDataService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;

namespace BookLoop.Services.Reports
{
	/// <summary>
	/// 商務資料查詢：只做「可翻成 SQL」的彙總與排序；
	/// 任何 ToString/格式化都移回記憶體端處理，避免 EF Core 無法翻譯。
	/// </summary>
	public class ShopReportDataService : IReportDataService
	{
		private readonly ShopDbContext _db;

		public ShopReportDataService(ShopDbContext db) => _db = db;

		/// <summary>
		/// 折線圖：依顆粒度彙總「總銷售金額」。
		/// 注意：
		///   - 不限制出版社時，沿用你原本的 Orders.TotalAmount 彙總（避免既有數字變動）
		///   - 有傳 publisherIds 時，改用「OrderDetails × Books」過濾 b.PublisherID 後加總
		/// </summary>
		public async Task<IReadOnlyList<ChartPoint>> GetSalesAmountSeriesAsync(
			DateTime start,
			DateTime end,
			string granularity = "day",
			int[]? excludeStatuses = null,
			int[]? publisherIds = null)
		{
			// 半開區間：[start, end)
			var endExclusive = end.Date.AddDays(1);

			// 例如 0=已取消；若你的系統是別的碼請調整（沿用你現有檔案註解）
			excludeStatuses ??= new[] { 0 };

			// ========== A. 無出版社限制：沿用舊邏輯（以 Orders.TotalAmount 彙總） ==========
			if (publisherIds is null || publisherIds.Length == 0)
			{
				var baseQ = _db.Orders
					.Where(o => o.OrderDate >= start && o.OrderDate < endExclusive)
					.Where(o => !excludeStatuses.Contains((int)o.Status));

				if (string.Equals(granularity, "year", StringComparison.OrdinalIgnoreCase))
				{
					var rows = await baseQ
						.GroupBy(o => o.OrderDate.Year)
						.Select(g => new { Year = g.Key, Amount = g.Sum(x => x.TotalAmount) })
						.OrderBy(x => x.Year)
						.ToListAsync();

					return rows.Select(x => new ChartPoint { Label = x.Year.ToString(), Value = x.Amount }).ToList();
				}

				if (string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase))
				{
					var rows = await baseQ
						.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
						.Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.TotalAmount) })
						.OrderBy(x => x.Year).ThenBy(x => x.Month)
						.ToListAsync();

					return rows.Select(x => new ChartPoint
					{
						Label = $"{x.Year:D4}-{x.Month:D2}",
						Value = x.Amount
					}).ToList();
				}

				// day（預設）
				{
					var rows = await baseQ
						.GroupBy(o => o.OrderDate.Date)
						.Select(g => new { Date = g.Key, Amount = g.Sum(x => x.TotalAmount) })
						.OrderBy(x => x.Date)
						.ToListAsync();

					return rows.Select(x => new ChartPoint
					{
						Label = x.Date.ToString("yyyy-MM-dd"),
						Value = x.Amount
					}).ToList();
				}
			}

			// ========== B. 有出版社限制：用明細 × 書籍過濾 PublisherID 後加總 ==========
			{
				var q = from od in _db.OrderDetails.AsNoTracking()
						join o in _db.Orders.AsNoTracking() on od.OrderID equals o.OrderID
						join b in _db.Books.AsNoTracking() on od.BookID equals b.BookID
						where o.OrderDate >= start && o.OrderDate < endExclusive
							  && !excludeStatuses.Contains((int)o.Status)
							  && publisherIds!.Contains(b.PublisherID)
						select new
						{
							o.OrderDate,
							Amount = (b.SalePrice ?? b.ListPrice) * od.Quantity
						};

				if (string.Equals(granularity, "year", StringComparison.OrdinalIgnoreCase))
				{
					var rows = await q
						.GroupBy(x => x.OrderDate.Year)
						.Select(g => new { Year = g.Key, Amount = g.Sum(x => x.Amount) })
						.OrderBy(x => x.Year)
						.ToListAsync();

					return rows.Select(x => new ChartPoint { Label = x.Year.ToString(), Value = x.Amount }).ToList();
				}

				if (string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase))
				{
					var rows = await q
						.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
						.Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) })
						.OrderBy(x => x.Year).ThenBy(x => x.Month)
						.ToListAsync();

					return rows.Select(x => new ChartPoint
					{
						Label = $"{x.Year:D4}-{x.Month:D2}",
						Value = x.Amount
					}).ToList();
				}

				// day（預設）
				{
					var rows = await q
						.GroupBy(x => x.OrderDate.Date)
						.Select(g => new { Date = g.Key, Amount = g.Sum(x => x.Amount) })
						.OrderBy(x => x.Date)
						.ToListAsync();

					return rows.Select(x => new ChartPoint
					{
						Label = x.Date.ToString("yyyy-MM-dd"),
						Value = x.Amount
					}).ToList();
				}
			}
		}

		/// <summary>
		/// 長條圖：一段期間內「銷售書籍排行」（以數量排序）。
		/// </summary>
		public async Task<IReadOnlyList<ChartPoint>> GetTopSoldBooksAsync(
			DateTime start,
			DateTime endInclusive,
			int topN = 10,
			int[]? excludeStatuses = null,
			int[]? publisherIds = null)
		{
			var endExclusive = endInclusive.Date.AddDays(1);
			excludeStatuses ??= new[] { 0 };

			// Orders + OrderDetails + Books，先在 DB 端完成彙總與排序
			var q =
				from od in _db.OrderDetails.AsNoTracking()
				join o in _db.Orders.AsNoTracking() on od.OrderID equals o.OrderID
				join b in _db.Books.AsNoTracking() on od.BookID equals b.BookID
				where o.OrderDate >= start
					  && o.OrderDate < endExclusive
					  && !excludeStatuses.Contains((int)o.Status)
				select new
				{
					b.Title,
					b.PublisherID,
					Qty = od.Quantity
				};

			if (publisherIds is { Length: > 0 })
				q = q.Where(x => publisherIds.Contains(x.PublisherID)); // ★ 直接用 Book.PublisherID 過濾

			var rows = await q
				.GroupBy(x => x.Title)
				.Select(g => new { Label = g.Key, Value = g.Sum(x => x.Qty) })
				.OrderByDescending(x => x.Value)
				.Take(topN <= 0 ? 10 : topN)
				.ToListAsync();

			return rows.Select(x => new ChartPoint { Label = x.Label, Value = (decimal)x.Value }).ToList();
		}

		/// <summary>
		/// 一段期間內「借閱書籍排行」（以借閱次數排序）。
		/// BorrowRecords → Listings（書名）
		/// </summary>
		public async Task<IReadOnlyList<ChartPoint>> GetTopBorrowBooksAsync(
			DateTime start,
			DateTime endInclusive,
			int topN = 10,
			int[]? publisherIds = null)
		{
			var endExclusive = endInclusive.Date.AddDays(1);
			topN = topN <= 0 ? 10 : Math.Min(topN, 50);

			var q =
				from br in _db.BorrowRecords.AsNoTracking()
				join l in _db.Listings.AsNoTracking() on br.ListingID equals l.ListingID
				where br.BorrowDate >= start && br.BorrowDate < endExclusive
				select new { l.Title, l.PublisherID };

			if (publisherIds is { Length: > 0 })
				q = q.Where(x => publisherIds.Contains(x.PublisherID)); // 與銷售邏輯一致：依出版社過濾

			var rows = await q
				.GroupBy(x => x.Title)
				.Select(g => new { Label = g.Key, Value = g.Count() })
				.OrderByDescending(x => x.Value)
				.Take(topN)
				.ToListAsync();

			return rows.Select(x => new ChartPoint { Label = x.Label, Value = (decimal)x.Value }).ToList();
		}
	}
}
