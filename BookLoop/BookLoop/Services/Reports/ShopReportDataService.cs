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
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetSalesAmountSeriesAsync(
            DateTime start,
            DateTime end,
            string granularity = "day",
            int[]? excludeStatuses = null,
            int? supplierId = null) // ★ 接收 int? supplierId
        {
            // 半開區間：[start, end)
            var endExclusive = end.Date.AddDays(1);
            excludeStatuses ??= new[] { 0 };

            // ========== 銷售報表基礎查詢：Orders/Details JOIN Books/Publishers ==========
            var q = from od in _db.OrderDetails.AsNoTracking()
                    join o in _db.Orders.AsNoTracking() on od.OrderID equals o.OrderID
                    join b in _db.Books.AsNoTracking() on od.BookID equals b.BookID
                    join p in _db.Publishers.AsNoTracking() on b.PublisherID equals p.PublisherID // ★ 引入 Publishers
                    where o.OrderDate >= start && o.OrderDate < endExclusive
                          && !excludeStatuses.Contains((int)o.Status)
                    select new
                    {
                        o.OrderDate,
                        p.SupplierID, // ★ 帶出 SupplierID
                        Amount = (b.SalePrice ?? b.ListPrice) * od.Quantity
                    };

            // ★ Data Scope 過濾：如果 supplierId 有值 (非 null)，則強制過濾
            if (supplierId.HasValue)
            {
                // 如果 supplierId=0，則直接過濾掉所有數據
                if (supplierId.Value == 0)
                {
                    return Array.Empty<ChartPoint>();
                }
                q = q.Where(x => x.SupplierID == supplierId.Value);
            }
            // 如果 supplierId 是 null，則不加過濾條件 (ALL)

            // ========== 分組與彙總 ==========
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

        /// <summary>
        /// 長條圖：一段期間內「銷售書籍排行」（以數量排序）。
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetTopSoldBooksAsync(
            DateTime start,
            DateTime endInclusive,
            int topN = 10,
            int[]? excludeStatuses = null,
            int? supplierId = null) // ★ 接收 int? supplierId
        {
            var endExclusive = endInclusive.Date.AddDays(1);
            excludeStatuses ??= new[] { 0 };

            // Orders + OrderDetails + Books + Publishers
            var q =
                from od in _db.OrderDetails.AsNoTracking()
                join o in _db.Orders.AsNoTracking() on od.OrderID equals o.OrderID
                join b in _db.Books.AsNoTracking() on od.BookID equals b.BookID
                join p in _db.Publishers.AsNoTracking() on b.PublisherID equals p.PublisherID
                where o.OrderDate >= start
                      && o.OrderDate < endExclusive
                      && !excludeStatuses.Contains((int)o.Status)
                select new
                {
                    BookID = b.BookID,
                    Title = b.Title,
                    Qty = od.Quantity,
                    p.SupplierID // ★ 帶出 SupplierID
                };

            // ★ Data Scope 過濾
            if (supplierId.HasValue)
            {
                if (supplierId.Value == 0)
                {
                    return Array.Empty<ChartPoint>();
                }
                q = q.Where(x => x.SupplierID == supplierId.Value);
            }

            var rows = await q
                .GroupBy(x => new { x.BookID, x.Title })
                .Select(g => new
                {
                    Label = g.Key.Title,
                    Value = g.Sum(x => x.Qty)
                })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToListAsync();

            return rows.Select(x => new ChartPoint { Label = x.Label, Value = (decimal)x.Value }).ToList();
        }

        /// <summary>
        /// 一段期間內「借閱書籍排行」（以借閱次數排序）。
        /// BorrowRecords → Listings → Publishers
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetTopBorrowBooksAsync(
            DateTime start,
            DateTime endInclusive,
            int topN = 10,
            int? supplierId = null) // ★ 接收 int? supplierId
        {
            var endExclusive = endInclusive.Date.AddDays(1);
            topN = topN <= 0 ? 10 : Math.Min(topN, 50);

            // BorrowRecords → Listings → Publishers
            var q =
                from br in _db.BorrowRecords.AsNoTracking()
                join l in _db.Listings.AsNoTracking() on br.ListingID equals l.ListingID
                join p in _db.Publishers.AsNoTracking() on l.PublisherID equals p.PublisherID
                where br.BorrowDate >= start && br.BorrowDate < endExclusive
                select new
                {
                    l.ListingID,
                    l.Title,
                    p.SupplierID // ★ 帶出 SupplierID
                };

            // ★ Data Scope 過濾
            if (supplierId.HasValue)
            {
                if (supplierId.Value == 0)
                {
                    return Array.Empty<ChartPoint>();
                }
                q = q.Where(x => x.SupplierID == supplierId.Value);
            }

            var rows = await q
                .GroupBy(x => new { x.ListingID, x.Title })
                .Select(g => new
                {
                    Label = g.Key.Title,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .Take(topN)
                .ToListAsync();

            return rows.Select(x => new ChartPoint { Label = x.Label, Value = x.Value }).ToList();
        }
    }
}