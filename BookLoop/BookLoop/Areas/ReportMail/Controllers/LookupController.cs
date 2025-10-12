using System;
using System.Linq;
using System.Threading.Tasks;
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

            var list = await query
                .Select(x => new { x.CategoryID, x.CategoryName })
                .Distinct()
                .OrderBy(x => x.CategoryName)
                .Select(x => new { value = x.CategoryID, text = x.CategoryName })
                .ToListAsync();

            return Json(list);
        }

        //[HttpGet]
        //public async Task<IActionResult> OrderStatuses()
        //{
        //    // 你的 Orders.Status 是 tinyint；這裡先給常見對照，找不到時顯示「狀態{code}」
        //    var map = new Dictionary<int, string> {
        //            { 0, "新訂單" }, { 1, "已付款" }, { 2, "已出貨" }, { 3, "已完成" }, { 4, "已取消" }
        //    };
        //
        //    var codes = await _shop.Orders
        //            .AsNoTracking()
        //            .Select(x => x.Status)
        //            .Distinct()
        //            .OrderBy(x => x)
        //            .ToListAsync();
        //
        //    var list = codes.Select(c => new {
        //            value = (int)c,
        //            text = map.TryGetValue((int)c, out var name) ? name : $"狀態{c}"
        //    });
        //    return Json(list);
        //}
    }
}