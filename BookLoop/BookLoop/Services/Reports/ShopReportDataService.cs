// Services/Reports/ShopReportDataService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Models;

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
        /// 注意：資料庫端只算出 Date/Year/Month 與 Amount；回到記憶體後才 Format Label。
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetSalesAmountSeriesAsync(
            DateTime start, DateTime end, string granularity = "day", int[]? excludeStatuses = null)
        {
            // 半開區間：[start, end)
            var endExclusive = end.Date.AddDays(1);

            // 例如 0=已取消；若你的系統是別的碼請調整
            excludeStatuses ??= new[] { 0 };

            // 先把共用的篩選寫好（注意 Status 若是 byte/enum，就轉成 int 比對）
            var baseQ = _db.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate < endExclusive)
                .Where(o => !excludeStatuses.Contains((int)o.Status));

            // 依顆粒度決定群組鍵與排序（都在資料庫端做）
            if (string.Equals(granularity, "year", StringComparison.OrdinalIgnoreCase))
            {
                var rows = await baseQ
                    .GroupBy(o => o.OrderDate.Year)
                    .Select(g => new { Year = g.Key, Amount = g.Sum(x => x.TotalAmount) })
                    .OrderBy(x => x.Year)
                    .ToListAsync();

                // 回到記憶體後才把 Label 轉字串
                return rows
                    .Select(x => new ChartPoint { Label = x.Year.ToString(), Value = x.Amount })
                    .ToList();
            }

            if (string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase))
            {
                var rows = await baseQ
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.TotalAmount) })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToListAsync();

                return rows
                    .Select(x => new ChartPoint
                    {
                        Label = $"{x.Year:D4}-{x.Month:D2}",
                        Value = x.Amount
                    })
                    .ToList();
            }

            // day（預設）
            {
                var rows = await baseQ
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.TotalAmount) })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return rows
                    .Select(x => new ChartPoint
                    {
                        Label = x.Date.ToString("yyyy-MM-dd"),
                        Value = x.Amount
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// 長條圖：一段期間內「銷售書籍排行」（以數量排序）。
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetTopSoldBooksAsync(
            DateTime start, DateTime endInclusive, int topN = 10, int[]? excludeStatuses = null)
        {
            var endExclusive = endInclusive.Date.AddDays(1);
            excludeStatuses ??= new[] { 0 };

            // Orders + OrderDetail + Books，先在 DB 端完成彙總與排序
            var rows =
                await (from od in _db.OrderDetails
                       join o in _db.Orders on od.OrderID equals o.OrderID
                       join b in _db.Books on od.BookID equals b.BookID
                       where o.OrderDate >= start && o.OrderDate < endExclusive
                             && !excludeStatuses.Contains((int)o.Status)
                       group od by b.Title into g
                       orderby g.Sum(x => x.Quantity) descending
                       select new
                       {
                           Title = g.Key,
                           Qty = g.Sum(x => x.Quantity)
                       })
                      .Take(topN)
                      .ToListAsync();

            // 回到記憶體端再組 ChartPoint（需要 decimal 就轉一下）
            return rows.Select(x => new ChartPoint
            {
                Label = x.Title,
                Value = (decimal)x.Qty
            }).ToList();
        }

        /// <summary>
        /// 圓餅圖：一段期間內「借閱書籍種類」排行（以借閱筆數計）。
        /// BorrowRecords → Listings → Categories
        /// </summary>
        public async Task<IReadOnlyList<ChartPoint>> GetTopBorrowCategoryAsync(
            DateTime start, DateTime endInclusive, int topN = 5)
        {
            var endExclusive = endInclusive.Date.AddDays(1);

            var rows =
                await (from br in _db.BorrowRecords
                       join l in _db.Listings on br.ListingID equals l.ListingID
                       join c in _db.Categories on l.CategoryID equals c.CategoryID
                       where br.BorrowDate >= start && br.BorrowDate < endExclusive
                       group br by c.CategoryName into g
                       orderby g.Count() descending
                       select new
                       {
                           Category = g.Key,
                           Cnt = g.Count()
                       })
                      .Take(topN)
                      .ToListAsync();

            return rows.Select(x => new ChartPoint
            {
                Label = x.Category,
                Value = (decimal)x.Cnt
            }).ToList();
        }
    }
}
