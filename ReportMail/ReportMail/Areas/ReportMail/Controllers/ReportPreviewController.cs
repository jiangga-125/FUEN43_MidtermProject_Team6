using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Shop;
using System.Text.Json;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    public class ReportPreviewController : Controller
    {
        private readonly ShopDbContext _shop;
        public ReportPreviewController(ShopDbContext shop) => _shop = shop;

        public class PreviewReq
        {
            public string? Category { get; set; } // line | bar | pie
            public List<FilterDraft>? Filters { get; set; }
        }
        public class FilterDraft
        {
            public string? FieldName { get; set; }
            public string? Operator { get; set; }
            public string? DataType { get; set; }
            public string? DefaultValue { get; set; }
            public string? Options { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> PreviewDraft([FromBody] PreviewReq req)
        {
            var cat = (req?.Category ?? "line").ToLowerInvariant();

            // 解析常見條件
            DateTime? dateFrom = null, dateTo = null;
            string gran = "day";
            List<int> categoryIds = new();
            int? priceMin = null, priceMax = null;
            int topFrom = 1, topTo = 10;
            int? pubMin = null, pubMax = null;

            foreach (var f in req?.Filters ?? new List<FilterDraft>())
            {
                var name = (f.FieldName ?? "").ToLowerInvariant();

                // 日期區間（OrderDate / BorrowDate / DateRange）
                if (name == "orderdate" || name == "borrowdate" || name == "daterange")
                {
                    // DefaultValue 期望 "yyyy-MM-dd~yyyy-MM-dd"；沒給就用近 30 天
                    if (!string.IsNullOrWhiteSpace(f.DefaultValue) && f.DefaultValue.Contains('~'))
                    {
                        var parts = f.DefaultValue.Split('~');
                        if (DateTime.TryParse(parts.ElementAtOrDefault(0), out var d1)) dateFrom = d1.Date;
                        if (DateTime.TryParse(parts.ElementAtOrDefault(1), out var d2)) dateTo = d2.Date.AddDays(1).AddTicks(-1);
                    }
                    // granulariy 也可從 DefaultValue="gran=month" 帶入
                    if (!string.IsNullOrWhiteSpace(f.DefaultValue) && f.DefaultValue.Contains("gran="))
                    {
                        var seg = f.DefaultValue.Split("gran=").Last().Trim();
                        if (seg is "day" or "month" or "year") gran = seg;
                    }
                }
                // 書籍種類
                else if (name == "categoryid" && f.Operator == "in" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
                    categoryIds = f.DefaultValue.Split(',')
                        .Select(s => int.TryParse(s, out var x) ? x : (int?)null)
                        .Where(x => x.HasValue).Select(x => x!.Value).ToList();
                }
                // 價位區間
                else if (name == "saleprice" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
                    var parts = f.DefaultValue.Split('-');
                    if (int.TryParse(parts.ElementAtOrDefault(0), out var a)) priceMin = a;
                    if (int.TryParse(parts.ElementAtOrDefault(1), out var b)) priceMax = b;
                }
                // 排名名次
                else if (name == "rank" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
                    var parts = f.DefaultValue.Split('-');
                    if (int.TryParse(parts.ElementAtOrDefault(0), out var a)) topFrom = a;
                    if (int.TryParse(parts.ElementAtOrDefault(1), out var b)) topTo = b;
                }
                // 出版年份（目前預覽不使用資料庫欄位，先解析保留）
                else if (name == "publishyear" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
                    var parts = f.DefaultValue.Split('-');
                    if (int.TryParse(parts.ElementAtOrDefault(0), out var a)) pubMin = a;
                    if (int.TryParse(parts.ElementAtOrDefault(1), out var b)) pubMax = b;
                }
            }

            dateFrom ??= DateTime.Today.AddDays(-29);
            dateTo ??= DateTime.Today;

            if (cat == "line")
            {
                // 銷售量（以 OrderDetail.Quantity 匯總）
                // 價位以 Book.SalePrice ?? Book.ListPrice 篩選
                var q = from od in _shop.OrderDetails
                        join o in _shop.Orders on od.OrderID equals o.OrderID
                        join b in _shop.Books on od.BookID equals b.BookID
                        where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
                        select new
                        {
                            o.OrderDate,
                            b.CategoryID,
                            Price = (b.SalePrice ?? b.ListPrice),
                            od.Quantity
                        };

                if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));
                if (priceMin.HasValue) q = q.Where(x => x.Price >= priceMin.Value);
                if (priceMax.HasValue) q = q.Where(x => x.Price <= priceMax.Value);

                if (gran == "month")
                {
                    var data = await q.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
                                      .Select(g => new { Label = g.Key.Year + "-" + g.Key.Month, V = g.Sum(x => (decimal)x.Quantity) })
                                      .OrderBy(x => x.Label).ToListAsync();
                    return Json(new { title = "銷售量(月)", labels = data.Select(x => x.Label), data = data.Select(x => x.V) });
                }
                else if (gran == "year")
                {
                    var data = await q.GroupBy(x => x.OrderDate.Year)
                                      .Select(g => new { Label = g.Key.ToString(), V = g.Sum(x => (decimal)x.Quantity) })
                                      .OrderBy(x => x.Label).ToListAsync();
                    return Json(new { title = "銷售量(年)", labels = data.Select(x => x.Label), data = data.Select(x => x.V) });
                }
                else // day
                {
                    var data = await q.GroupBy(x => x.OrderDate.Date)
                                      .Select(g => new { Label = g.Key, V = g.Sum(x => (decimal)x.Quantity) })
                                      .OrderBy(x => x.Label).ToListAsync();
                    return Json(new { title = "銷售量(日)", labels = data.Select(x => x.Label.ToString("yyyy-MM-dd")), data = data.Select(x => x.V) });
                }
            }
            else if (cat == "bar")
            {
                // 銷售排行（以書名為維度），保護上限 50
                var q = from od in _shop.OrderDetails
                        join o in _shop.Orders on od.OrderID equals o.OrderID
                        join b in _shop.Books on od.BookID equals b.BookID
                        where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
                        select new
                        {
                            b.Title,
                            b.CategoryID,
                            Price = (b.SalePrice ?? b.ListPrice),
                            od.Quantity
                        };

                if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));
                if (priceMin.HasValue) q = q.Where(x => x.Price >= priceMin.Value);
                if (priceMax.HasValue) q = q.Where(x => x.Price <= priceMax.Value);

                var take = Math.Max(1, Math.Min(50, topTo));
                var data = await q.GroupBy(x => x.Title)
                                  .Select(g => new { Label = g.Key, V = g.Sum(x => (decimal)x.Quantity) })
                                  .OrderByDescending(x => x.V)
                                  .Take(take)
                                  .ToListAsync();

                return Json(new { title = $"銷售排行 Top {take}", labels = data.Select(x => x.Label), data = data.Select(x => x.V) });
            }
            else // pie
            {
                // 借閱量組成（以 Listing.CategoryID 匯總）
                var q = from br in _shop.BorrowRecords
                        join l in _shop.Listings on br.ListingID equals l.ListingID
                        where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
                        select new { l.CategoryID, br.RecordID };

                if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));

                var data = await q.GroupBy(x => x.CategoryID)
                                  .Select(g => new { Label = "分類 " + g.Key, V = g.Count() })
                                  .OrderByDescending(x => x.V)
                                  .Take(5)
                                  .ToListAsync();

                return Json(new { title = "借閱種類組成", labels = data.Select(x => x.Label), data = data.Select(x => (decimal)x.V) });
            }
        }
    }
}
