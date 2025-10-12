using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Route("ReportMail/[controller]/[action]")]
    public class LookupController : Controller
    {
        private readonly ShopDbContext _shop;
        public LookupController(ShopDbContext shop) => _shop = shop;

        [HttpGet]
        public async Task<IActionResult> Categories(
            [FromQuery] string? kind,
            [FromQuery(Name = "baseKind")] string? baseKind,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end)
        {
            var source = (baseKind ?? kind ?? "sales").Trim().ToLowerInvariant();
            var startDate = start?.Date;
            var endDate = end?.Date;

            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            {
                return Json(Array.Empty<object>());
            }

            DateTime? endExclusive = endDate?.AddDays(1);

            IQueryable<Category> query;

            if (source == "borrow")
            {
                var borrowQuery = from record in _shop.BorrowRecords.AsNoTracking()
                                  join listing in _shop.Listings.AsNoTracking() on record.ListingID equals listing.ListingID
                                  join category in _shop.Categories.AsNoTracking() on listing.CategoryID equals category.CategoryID
                                  select new { record.BorrowDate, Category = category };

                if (startDate.HasValue)
                {
                    borrowQuery = borrowQuery.Where(x => x.BorrowDate >= startDate.Value);
                }

                if (endExclusive.HasValue)
                {
                    borrowQuery = borrowQuery.Where(x => x.BorrowDate < endExclusive.Value);
                }

                query = borrowQuery.Select(x => x.Category);
            }
            else
            {
                var salesQuery = from detail in _shop.OrderDetails.AsNoTracking()
                                 join order in _shop.Orders.AsNoTracking() on detail.OrderID equals order.OrderID
                                 join book in _shop.Books.AsNoTracking() on detail.BookID equals book.BookID
                                 join category in _shop.Categories.AsNoTracking() on book.CategoryID equals category.CategoryID
                                 select new { order.OrderDate, order.Status, Category = category };

                if (startDate.HasValue)
                {
                    salesQuery = salesQuery.Where(x => x.OrderDate >= startDate.Value);
                }

                if (endExclusive.HasValue)
                {
                    salesQuery = salesQuery.Where(x => x.OrderDate < endExclusive.Value);
                }

                // 預設排除已取消的訂單（狀態 0），與既有報表邏輯一致。
                salesQuery = salesQuery.Where(x => x.Status != 0);

                query = salesQuery.Select(x => x.Category);
            }

            // 先取「有紀錄」的唯一 CategoryID（排除 0）
            var idQuery = query
                .Select(c => c.CategoryID)
                .Where(id => id != 0)
                .Distinct();

            // 再回到 Categories 表把名稱撈齊（由 DB 來排序）
            var result = await _shop.Categories.AsNoTracking()
                .Where(c => idQuery.Contains(c.CategoryID))
                .Select(c => new { value = c.CategoryID, text = c.CategoryName })
                .OrderBy(x => x.text)
                .ToListAsync();

            return Json(result);

        }

        // 依「所選日期區間」+「已選書籍種類」回傳排行上限：
        //   sales → distinct BookID；borrow → distinct ListingID（用 Listings.CategoryID 篩）
        [HttpPost]
        public async Task<IActionResult> MaxRank([FromBody] MaxRankRequest req)
        {
            var kind = (req?.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // 解析日期（含當日）
            DateTime? startDate = null, endExclusive = null;
            var dateFilter = req?.Filters?.FirstOrDefault(f =>
                string.Equals(f.FieldName, "OrderDate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.FieldName, "BorrowDate", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(dateFilter?.ValueJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(dateFilter!.ValueJson!);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("from", out var fromEl) &&
                        DateTime.TryParse(fromEl.GetString(), out var fromDt))
                        startDate = fromDt.Date;
                    if (root.TryGetProperty("to", out var toEl) &&
                        DateTime.TryParse(toEl.GetString(), out var toDt))
                        endExclusive = toDt.Date.AddDays(1); // < end+1（含當日）
                }
                catch { }
            }

            // 解析已選的 CategoryIDs（可多選）
            var categoryIds = new List<int>();
            var catFilter = req?.Filters?.FirstOrDefault(f =>
                string.Equals(f.FieldName, "CategoryID", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(catFilter?.ValueJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(catFilter!.ValueJson!);
                    if (doc.RootElement.TryGetProperty("values", out var arr))
                        foreach (var x in arr.EnumerateArray())
                            if (x.TryGetInt32(out var id) && id > 0) categoryIds.Add(id);
                }
                catch { }
            }

            int count;

            if (kind == "borrow")
            {
                // 借閱：BorrowRecords × Listings（用 Listings.CategoryID 直接過濾）
                var q = from br in _shop.BorrowRecords.AsNoTracking()
                        join l in _shop.Listings.AsNoTracking() on br.ListingID equals l.ListingID
                        select new { br.BorrowDate, l.CategoryID, br.ListingID };

                if (startDate.HasValue) q = q.Where(x => x.BorrowDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.BorrowDate < endExclusive.Value);
                if (categoryIds.Count > 0) q = q.Where(x => categoryIds.Contains(x.CategoryID));

                count = await q.Select(x => x.ListingID).Distinct().CountAsync();
            }
            else
            {
                // 銷售：OrderDetails × Orders × Books（Books.CategoryID 過濾；Orders.Status != 0）
                var q = from d in _shop.OrderDetails.AsNoTracking()
                        join o in _shop.Orders.AsNoTracking() on d.OrderID equals o.OrderID
                        join b in _shop.Books.AsNoTracking() on d.BookID equals b.BookID
                        where o.Status != 0
                        select new { o.OrderDate, b.CategoryID, d.BookID };

                if (startDate.HasValue) q = q.Where(x => x.OrderDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.OrderDate < endExclusive.Value);
                if (categoryIds.Count > 0) q = q.Where(x => categoryIds.Contains(x.CategoryID));

                count = await q.Select(x => x.BookID).Distinct().CountAsync();
            }

            // 需要更大上限可把 100 調整或拿掉
            var maxRank = Math.Max(1, Math.Min(100, count));
            return Json(new { maxRank });
        }

        // === Add: 與前端 payload 對齊的模型 ===
        public sealed class MaxRankRequest
        {
            public string? BaseKind { get; set; }           // "sales" / "borrow"
            public List<FilterItem>? Filters { get; set; }  // 由 buildFilters() 送上來
        }
        public sealed class FilterItem
        {
            public string? FieldName { get; set; }          // 例如 OrderDate / BorrowDate / CategoryID / ...
            public string? ValueJson { get; set; }          // JSON（e.g. {"from":"2025-10-01","to":"2025-10-12"}）
        }

    }
}