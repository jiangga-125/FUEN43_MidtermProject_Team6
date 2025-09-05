using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Shop;
using System.Text.Json;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
    public class ReportPreviewController : Controller
    {
        private readonly ShopDbContext _shop;
        public ReportPreviewController(ShopDbContext shop) => _shop = shop;

        public class PreviewReq
        {
			public string? Category { get; set; }  // line|bar|pie（目前用 line）
			public string? BaseKind { get; set; }  // sales|borrow|orders
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


		private sealed class YMVal { public int Year { get; set; } public int Month { get; set; } public decimal V { get; set; } }
		private sealed class YVal { public int Year { get; set; } public decimal V { get; set; } }
		private sealed class DVal { public DateTime Day { get; set; } public decimal V { get; set; } }


        [HttpPost]
        public async Task<IActionResult> PreviewDraft([FromBody] PreviewReq req)
        {
			try
			{

				var baseKind = (req?.BaseKind ?? "sales").ToLowerInvariant();
				var cat = (req?.Category ?? "line").ToLowerInvariant(); // 目前都是 line

				var debug = new Dictionary<string, object?>();
				debug["baseKind"] = req?.BaseKind;
				debug["category"] = req?.Category;

				// 共用條件
				DateTime dateFrom, dateTo;
            string gran = "day";
				List<int> categoryIds = new();       // 空 = 全部
            int? priceMin = null, priceMax = null;
				int topTo = 10;                      // 柱/餅才用得到
				List<int> exclStatus = new();        // 訂單要排除的狀態
				List<int> inclStatus = new();		 // 訂單要包含的狀態
				string orderMetric = "amount";       // amount | count
				decimal? orderAmtMin = null, orderAmtMax = null;

				// 解析前端 filters
				dateFrom = DateTime.Today.AddDays(-29);
				dateTo = DateTime.Today;
				foreach (var f in req?.Filters ?? new())
            {
                var name = (f.FieldName ?? "").ToLowerInvariant();
					if (name is "orderdate" or "borrowdate" or "daterange")
					{
						if (!string.IsNullOrWhiteSpace(f.DefaultValue))
						{
							var tokens = f.DefaultValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

							// 解析粒度
							foreach (var t in tokens)
                {
								var kv = t.Split('=', 2, StringSplitOptions.TrimEntries);
								if (kv.Length == 2 && kv[0].Equals("gran", StringComparison.OrdinalIgnoreCase))
                    {
									var g = kv[1].ToLowerInvariant();
									if (g is "day" or "month" or "year") gran = g;
                    }
							}

							// 解析日期範圍
							var rangePart = tokens.FirstOrDefault(x => x.Contains('~')) ?? f.DefaultValue;
							var rp = rangePart.Split('~', StringSplitOptions.TrimEntries);
							if (rp.Length >= 1 && DateTime.TryParse(rp[0], out var d1)) dateFrom = d1.Date;
							if (rp.Length >= 2)
                    {
								var right = rp[1].Split('=', 2)[0]; // 去掉 ;gran=...
								if (DateTime.TryParse(right, out var d2))
									dateTo = d2.Date.AddDays(1).AddTicks(-1); // end of day
                    }


							debug["dateFrom"] = dateFrom;
							debug["dateTo"] = dateTo;
							debug["gran"] = gran;


                }
					}
                else if (name == "categoryid" && f.Operator == "in" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
						// 空字串代表「全部」→ 直接忽略此條件
						var tmp = f.DefaultValue.Split(',')
							.Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => int.TryParse(s, out var x) ? x : (int?)null)
                        .Where(x => x.HasValue).Select(x => x!.Value).ToList();
						if (tmp.Count > 0) categoryIds = tmp;
                }
                // 價位區間
                else if (name == "saleprice" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
						var p = f.DefaultValue.Split('-');
						if (int.TryParse(p.ElementAtOrDefault(0), out var a)) priceMin = a;
						if (int.TryParse(p.ElementAtOrDefault(1), out var b)) priceMax = b;
                }
                // 排名名次
                else if (name == "rank" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
						var p = f.DefaultValue.Split('-');
						if (int.TryParse(p.ElementAtOrDefault(1), out var t)) topTo = Math.Clamp(t, 1, 50);
                }
					else if (name == "metric" && !string.IsNullOrWhiteSpace(f.DefaultValue))
                {
						orderMetric = f.DefaultValue.ToLowerInvariant() == "count" ? "count" : "amount";
                }
					else if (name == "orderstatus" && f.Operator == "in" && !string.IsNullOrWhiteSpace(f.DefaultValue))
					{
						inclStatus = f.DefaultValue.Split(',')
							.Select(s => int.TryParse(s, out var x) ? x : (int?)null)
							.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            }
					else if (name == "excludestatus" && f.Operator == "in" && !string.IsNullOrWhiteSpace(f.DefaultValue))
					{
						exclStatus = f.DefaultValue.Split(',')
							.Select(s => int.TryParse(s, out var x) ? x : (int?)null)
							.Where(x => x.HasValue).Select(x => x!.Value).ToList();
					}
					else if (name == "orderamount" && f.Operator == "between" && !string.IsNullOrWhiteSpace(f.DefaultValue))
					{
						var p = f.DefaultValue.Split('-');
						if (decimal.TryParse(p.ElementAtOrDefault(0), out var a)) orderAmtMin = a;
						if (decimal.TryParse(p.ElementAtOrDefault(1), out var b)) orderAmtMax = b;
					}
				}

				// 工具：補零序列
				static IEnumerable<DateTime> EachDay(DateTime s, DateTime e)
				{
					for (var d = s.Date; d <= e.Date; d = d.AddDays(1)) yield return d;
				}
				static IEnumerable<(int y, int m)> EachMonth(DateTime s, DateTime e)
				{
					var cur = new DateTime(s.Year, s.Month, 1);
					var end = new DateTime(e.Year, e.Month, 1);
					while (cur <= end) { yield return (cur.Year, cur.Month); cur = cur.AddMonths(1); }
				}
				static IEnumerable<int> EachYear(DateTime s, DateTime e)
				{
					for (int y = s.Year; y <= e.Year; y++) yield return y;
				}

				// 預設最近 30 天
				if (dateFrom == default) dateFrom = DateTime.Today.AddDays(-29);
				if (dateTo == default) dateTo = DateTime.Today.AddDays(1).AddTicks(-1);

				// 使用者若把 from/to 反了，幫他交換
				if (dateFrom > dateTo) (dateFrom, dateTo) = (dateTo, dateFrom);

				debug["dateFrom"] = dateFrom;
				debug["dateTo"] = dateTo;
				debug["gran"] = gran;



				// ===== 分支：sales / borrow / orders =====
				if (baseKind == "sales")
            {
                // 銷售量（以 OrderDetail.Quantity 匯總）
                // 價位以 Book.SalePrice ?? Book.ListPrice 篩選
                var q = from od in _shop.OrderDetails
                        join o in _shop.Orders on od.OrderID equals o.OrderID
                        join b in _shop.Books on od.BookID equals b.BookID
                        where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
							select new { o.OrderDate, b.CategoryID, Price = (b.SalePrice ?? b.ListPrice), od.Quantity };

					debug["salesRawCount"] = await q.CountAsync();
					debug["ordersMinDate"] = await _shop.Orders.MinAsync(o => (DateTime?)o.OrderDate);
					debug["ordersMaxDate"] = await _shop.Orders.MaxAsync(o => (DateTime?)o.OrderDate);



                if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));
                if (priceMin.HasValue) q = q.Where(x => x.Price >= priceMin.Value);
                if (priceMax.HasValue) q = q.Where(x => x.Price <= priceMax.Value);

                if (gran == "month")
                {
						var raw = await q.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
										 .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (decimal)x.Quantity) })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => (k.Year, k.Month), v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var (y, m) in EachMonth(dateFrom, dateTo))
						{
							labels.Add($"{y}-{m:00}");
							data.Add(dict.TryGetValue((y, m), out var v) ? v : 0m);
                }
						return Json(new { title = "書籍銷售量（月）", labels, data, debug });
					}
                else if (gran == "year")
                {
						var raw = await q.GroupBy(x => x.OrderDate.Year)
										 .Select(g => new { Year = g.Key, V = g.Sum(x => (decimal)x.Quantity) })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => k.Year, v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var y in EachYear(dateFrom, dateTo))
                {
							labels.Add(y.ToString());
							data.Add(dict.TryGetValue(y, out var v) ? v : 0m);
                }
						return Json(new { title = "書籍銷售量（年）", labels, data, debug });
            }
					else // day
            {
						// 以 年/月/日 拆欄位分組，避免 DateTime.Date 轉譯差異
						var raw = await q.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month, x.OrderDate.Day })
										 .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, V = g.Sum(x => (decimal)x.Quantity) })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => (k.Year, k.Month, k.Day), v => v.V);

						var labels = new List<string>();
						var data = new List<decimal>();
						for (var d = dateFrom.Date; d <= dateTo.Date; d = d.AddDays(1))
                        {
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue((d.Year, d.Month, d.Day), out var v) ? v : 0m);
						}

						// （可留著）幫你在 Console 看原始彙總結果
						debug["salesDayKeys"] = raw.Select(r => $"{r.Year}-{r.Month:00}-{r.Day:00}").ToArray();
						debug["salesDayVals"] = raw.Select(r => r.V).ToArray();

						return Json(new { title = "書籍銷售量（日）", labels, data, debug });
					}

                return Json(new { title = $"銷售排行 Top {take}", labels = data.Select(x => x.Label), data = data.Select(x => x.V) });
            }
				else if (baseKind == "borrow")
            {
                // 借閱量組成（以 Listing.CategoryID 匯總）
                var q = from br in _shop.BorrowRecords
                        join l in _shop.Listings on br.ListingID equals l.ListingID
                        where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
							select new { br.BorrowDate, l.CategoryID };

					debug["dateFrom"] = dateFrom;
					debug["dateTo"] = dateTo;
					debug["gran"] = gran;


                if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));

					if (gran == "month")
					{
						var raw = await q.GroupBy(x => new { x.BorrowDate.Year, x.BorrowDate.Month })
										 .Select(g => new { g.Key.Year, g.Key.Month, V = g.Count() })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => (k.Year, k.Month), v => (decimal)v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var (y, m) in EachMonth(dateFrom, dateTo))
						{
							labels.Add($"{y}-{m:00}");
							data.Add(dict.TryGetValue((y, m), out var v) ? v : 0m);
						}
						return Json(new { title = "書籍借閱量（月）", labels, data, debug });
					}
					else if (gran == "year")
					{
						var raw = await q.GroupBy(x => x.BorrowDate.Year)
										 .Select(g => new { Year = g.Key, V = g.Count() })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => k.Year, v => (decimal)v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var y in EachYear(dateFrom, dateTo))
						{
							labels.Add(y.ToString());
							data.Add(dict.TryGetValue(y, out var v) ? v : 0m);
						}
						return Json(new { title = "書籍借閱量（年）", labels, data, debug });
					}
					else // day
					{
						var raw = await q.GroupBy(x => new { x.BorrowDate.Year, x.BorrowDate.Month, x.BorrowDate.Day })
										 .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, V = g.Count() })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => (k.Year, k.Month, k.Day), v => (decimal)v.V);

						var labels = new List<string>();
						var data = new List<decimal>();
						for (var d = dateFrom.Date; d <= dateTo.Date; d = d.AddDays(1))
						{
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue((d.Year, d.Month, d.Day), out var v) ? v : 0m);
						}

						debug["borrowDayKeys"] = raw.Select(r => $"{r.Year}-{r.Month:00}-{r.Day:00}").ToArray();
						debug["borrowDayVals"] = raw.Select(r => (decimal)r.V).ToArray();

						return Json(new { title = "書籍借閱量（日）", labels, data, debug });
					}

				}
				else // orders
				{
					var q = _shop.Orders.AsNoTracking().Where(o => o.OrderDate >= dateFrom && o.OrderDate <= dateTo);

					if (inclStatus.Any())
						q = q.Where(o => inclStatus.Contains((int)o.Status));
					else if (exclStatus.Any())
						q = q.Where(o => !exclStatus.Contains((int)o.Status));
					if (orderAmtMin.HasValue) q = q.Where(o => o.TotalAmount >= orderAmtMin.Value);
					if (orderAmtMax.HasValue) q = q.Where(o => o.TotalAmount <= orderAmtMax.Value);

					debug["ordersRawCount"] = await q.CountAsync();
					debug["ordersMinDate"] = await _shop.Orders.MinAsync(o => (DateTime?)o.OrderDate);
					debug["ordersMaxDate"] = await _shop.Orders.MaxAsync(o => (DateTime?)o.OrderDate);


					if (gran == "month")
					{
						List<YMVal> raw;
						if (orderMetric == "count")
						{
							raw = await q.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
										 .Select(g => new YMVal { Year = g.Key.Year, Month = g.Key.Month, V = (decimal)g.Count() })
										 .ToListAsync();
						}
						else
						{
							raw = await q.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
										 .Select(g => new YMVal { Year = g.Key.Year, Month = g.Key.Month, V = g.Sum(x => x.TotalAmount) })
										 .ToListAsync();
						}

						var dict = raw.ToDictionary(k => (k.Year, k.Month), v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var (y, m) in EachMonth(dateFrom, dateTo))
						{
							labels.Add($"{y}-{m:00}");
							data.Add(dict.TryGetValue((y, m), out var v) ? v : 0m);
						}
						return Json(new { title = orderMetric == "count" ? "訂單筆數（月）" : "總銷售金額（月）", labels, data, debug });
					}
					else if (gran == "year")
					{
						List<YVal> raw;
						if (orderMetric == "count")
						{
							raw = await q.GroupBy(o => o.OrderDate.Year)
										 .Select(g => new YVal { Year = g.Key, V = (decimal)g.Count() })
										 .ToListAsync();
						}
						else
						{
							raw = await q.GroupBy(o => o.OrderDate.Year)
										 .Select(g => new YVal { Year = g.Key, V = g.Sum(x => x.TotalAmount) })
										 .ToListAsync();
						}

						var dict = raw.ToDictionary(k => k.Year, v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var y in EachYear(dateFrom, dateTo))
						{
							labels.Add(y.ToString());
							data.Add(dict.TryGetValue(y, out var v) ? v : 0m);
						}
						return Json(new { title = orderMetric == "count" ? "訂單筆數（年）" : "總銷售金額（年）", labels, data, debug });
					}
					else // day
					{
						List<DVal> raw;
						if (orderMetric == "count")
						{
							// 以 年/月/日 拆欄位分組
							raw = await q.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day })
										 .Select(g => new DVal { Day = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day), V = (decimal)g.Count() })
										 .ToListAsync();
						}
						else
						{
							raw = await q.GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day })
										 .Select(g => new DVal { Day = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day), V = g.Sum(x => x.TotalAmount) })
                                  .ToListAsync();
						}

						var dict = raw.ToDictionary(k => (k.Day.Year, k.Day.Month, k.Day.Day), v => v.V);

						var labels = new List<string>();
						var data = new List<decimal>();
						for (var d = dateFrom.Date; d <= dateTo.Date; d = d.AddDays(1))
						{
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue((d.Year, d.Month, d.Day), out var v) ? v : 0m);
						}

						debug["ordersDayKeys"] = raw.Select(r => r.Day.ToString("yyyy-MM-dd")).ToArray();
						debug["ordersDayVals"] = raw.Select(r => r.V).ToArray();

						return Json(new { title = orderMetric == "count" ? "訂單筆數（日）" : "總銷售金額（日）", labels, data, debug });
					}


				}
			}
			catch (Exception ex)
			{
				// 不讓前端因 500 無法 parse；固定回 JSON
				Response.StatusCode = 400;
				return Json(new { error = "Preview failed", detail = ex.Message });
            }

        }

    }
}
