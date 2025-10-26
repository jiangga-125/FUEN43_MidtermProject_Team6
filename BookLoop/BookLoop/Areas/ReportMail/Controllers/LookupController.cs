using BookLoop.Data;
using BookLoop.Models;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ReportMail.Areas.ReportMail.Controllers
{
    public class LookupController : ReportMailAreaController
    {
        private readonly ShopDbContext _shop;
        public LookupController(ShopDbContext shop) => _shop = shop;

        [HttpGet]
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> Categories(string? kind, [FromQuery(Name = "baseKind")] string? baseKind,
                                            DateTime? start, DateTime? end)
        {
            var (canAll, mySupplierId) = await GetScopeAsync();
            if (!canAll && mySupplierId is null) return Forbid();

            var source = (baseKind ?? kind ?? "sales").Trim().ToLowerInvariant();
            var startDate = start?.Date;
            DateTime? endExclusive = end.HasValue ? end.Value.Date.AddDays(1) : (DateTime?)null;

            IQueryable<int> idQuery;

            if (source == "borrow")
            {
                // BorrowRecords → Listings → Publishers(拿 SupplierID) → Categories
                var q = from record in _shop.BorrowRecords.AsNoTracking()
                        join listing in _shop.Listings.AsNoTracking() on record.ListingID equals listing.ListingID
                        join pub in _shop.Publishers.AsNoTracking() on listing.PublisherID equals pub.PublisherID
                        join category in _shop.Categories.AsNoTracking() on listing.CategoryID equals category.CategoryID
                        select new { record.BorrowDate, SupplierID = pub.SupplierID, category.CategoryID };

                if (startDate.HasValue) q = q.Where(x => x.BorrowDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.BorrowDate < endExclusive.Value);
                if (!canAll) q = q.Where(x => x.SupplierID == mySupplierId);

                idQuery = q.Select(x => x.CategoryID);
            }
            else
            {
                // OrderDetails → Orders → Books → Publishers(拿 SupplierID) → Categories
                var q = from d in _shop.OrderDetails.AsNoTracking()
                        join o in _shop.Orders.AsNoTracking() on d.OrderID equals o.OrderID
                        join b in _shop.Books.AsNoTracking() on d.BookID equals b.BookID
                        join pub in _shop.Publishers.AsNoTracking() on b.PublisherID equals pub.PublisherID
                        join category in _shop.Categories.AsNoTracking() on b.CategoryID equals category.CategoryID
                        where o.Status != 0
                        select new { o.OrderDate, SupplierID = pub.SupplierID, category.CategoryID };

                if (startDate.HasValue) q = q.Where(x => x.OrderDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.OrderDate < endExclusive.Value);
                if (!canAll) q = q.Where(x => x.SupplierID == mySupplierId);

                idQuery = q.Select(x => x.CategoryID);
            }

            var ids = await idQuery.Where(id => id != 0).Distinct().ToListAsync();

            var result = await _shop.Categories.AsNoTracking()
                .Where(c => ids.Contains(c.CategoryID))
                .Select(c => new { value = c.CategoryID, text = c.CategoryName })
                .OrderBy(x => x.text)
                .ToListAsync();

            return Json(result);
        }


        // 依「所選日期區間」+「已選書籍種類」回傳排行上限：
        //   sales → distinct BookID；borrow → distinct ListingID（用 Listings.CategoryID 篩）
        [HttpPost]
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> MaxRank([FromBody] MaxRankRequest req)
        {
            var (canAll, mySupplierId) = await GetScopeAsync();
            if (!canAll && mySupplierId is null) return Forbid();

            var source = (req?.BaseKind ?? "sales").Trim().ToLowerInvariant();

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

            if (source == "borrow")
            {
                var q = from br in _shop.BorrowRecords.AsNoTracking()
                        join l in _shop.Listings.AsNoTracking() on br.ListingID equals l.ListingID
                        join pub in _shop.Publishers.AsNoTracking() on l.PublisherID equals pub.PublisherID // ★ Supplier
                        select new { br.BorrowDate, l.CategoryID, br.ListingID, SupplierID = pub.SupplierID };

                if (startDate.HasValue) q = q.Where(x => x.BorrowDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.BorrowDate < endExclusive.Value);
                if (categoryIds.Count > 0) q = q.Where(x => categoryIds.Contains(x.CategoryID));
                if (!canAll) q = q.Where(x => x.SupplierID == mySupplierId);

                count = await q.Select(x => x.ListingID).Distinct().CountAsync();
            }
            else
            {
                var q = from d in _shop.OrderDetails.AsNoTracking()
                        join o in _shop.Orders.AsNoTracking() on d.OrderID equals o.OrderID
                        join b in _shop.Books.AsNoTracking() on d.BookID equals b.BookID
                        join pub in _shop.Publishers.AsNoTracking() on b.PublisherID equals pub.PublisherID // ★ Supplier
                        where o.Status != 0
                        select new { o.OrderDate, b.CategoryID, d.BookID, SupplierID = pub.SupplierID };

                if (startDate.HasValue) q = q.Where(x => x.OrderDate >= startDate.Value);
                if (endExclusive.HasValue) q = q.Where(x => x.OrderDate < endExclusive.Value);
                if (categoryIds.Count > 0) q = q.Where(x => categoryIds.Contains(x.CategoryID));
                if (!canAll) q = q.Where(x => x.SupplierID == mySupplierId);

                count = await q.Select(x => x.BookID).Distinct().CountAsync();
            }

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
        //取 scope
        private async Task<(bool canAll, int? mySupplierId)> GetScopeAsync()
        {
            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAll = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Data.All")).Succeeded;
            if (canAll) return (true, null);

            var s = User.FindFirst("supplier")?.Value;
            if (int.TryParse(s, out var sid)) return (false, sid);
            return (false, null);// 沒有 supplier claim ⇒ 視為無權
        }

    }
}