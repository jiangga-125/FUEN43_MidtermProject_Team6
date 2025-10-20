using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class ReportPreviewController : Controller
	{
		private readonly ShopDbContext _shop;
		public ReportPreviewController(ShopDbContext shop) => _shop = shop;

		// ==== 前端請求 DTO（支援新舊兩種欄位）====
		public class PreviewReq
		{
			public string? Category { get; set; }   // line|bar|pie（目前用 line）
			public string? BaseKind { get; set; }   // sales|borrow|orders
			public List<FilterDraft>? Filters { get; set; }
		}

		public class FilterDraft
		{
			public string? FieldName { get; set; }   // OrderDate / BorrowDate / CategoryID / SalePrice / OrderStatus / OrderAmount / Metric...
			public string? Operator { get; set; }    // between / in / eq...
			public string? DataType { get; set; }    // date / select / number...
													 // 新：結構化的值（建議使用）
			public string? ValueJson { get; set; }
			// 舊：字串值（相容舊版 DefaultValue）
			public string? DefaultValue { get; set; }
			public string? Options { get; set; }
		}

		// 繪圖用小型資料結構
		private sealed class YMVal { public int Year { get; set; } public int Month { get; set; } public decimal V { get; set; } }
		private sealed class YVal { public int Year { get; set; } public decimal V { get; set; } }
		private sealed class DVal { public DateTime Day { get; set; } public decimal V { get; set; } }

		// 反序列化 ValueJson 用
		private sealed record DateRangeV(string? from, string? to, string? gran);
		private sealed record InV(int[]? values);
		private sealed record RangeV(decimal? min, decimal? max);
		private sealed record ValueV(string? value);
		// 供 bar/pie 解析
		private sealed record RankRangeV(int? from, int? to);
		private sealed record DecadeV(int? fromYear, int? toYear);

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
			for (var y = s.Year; y <= e.Year; y++) yield return y;
		}

		[HttpPost]
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> PreviewDraft([FromBody] PreviewReq req)
		{
			try
			{
				// ===== ReportMail DataScope: All / ByPublisher =====
				var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
				var canAll = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Data.All")).Succeeded;
				var canPub = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Data.ByPublisher")).Succeeded;
				if (!canAll && !canPub) return Forbid();

				// 取書商 supplierId（先讀 claims 的 "supplier"，沒有就查 SUPPLIER_USERS）
				int GetSupplierIdOrThrow()
				{
					var s = User.FindAll("supplier").FirstOrDefault()?.Value;
					if (int.TryParse(s, out var cid)) return cid;

					var uidStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
							  ?? User.FindFirst("uid")?.Value;
					if (!int.TryParse(uidStr, out var uid)) throw new Exception("No user id in claims.");

					var sid = _shop.SupplierUsers
								  .Where(x => x.UserID == uid)
								  .Select(x => (int?)x.SupplierID)
								  .FirstOrDefault();
					if (!sid.HasValue) throw new Exception("No supplier mapping for user.");
					return sid.Value;
				}

				var cat = (req.Category ?? "line").Trim().ToLowerInvariant();        // line/bar/pie（圖表型態）
				var baseKind = (req.BaseKind ?? "sales").Trim().ToLowerInvariant();  // sales/borrow/orders（資料來源）

				// ===== 共用條件變數 =====
				DateTime dateFrom = default, dateTo = default;
				string gran = "day";
				List<int> categoryIds = new();       // 空 = 全部
				int? priceMin = null, priceMax = null;
				int topTo = 10;                      // bar/pie 時用（相容）
				int rankFrom = 1, rankTo = topTo;   // bar/pie 會用到；預設 1~topTo
				int? decadeFrom = null, decadeTo = null; // 借閱用（預留出版年份）

				// 訂單狀態（新：包含式）+ 舊：排除式（相容）
				var inclStatus = new List<int>();
				var exclStatus = new List<int>();
				bool statusSpecifiedByInclude = false;
				string orderMetric = "amount";       // amount | count
				decimal? orderAmtMin = null, orderAmtMax = null;

				// ===== 解析前端 filters（優先 ValueJson，退回 DefaultValue）=====
				foreach (var f in req?.Filters ?? new())
				{
					var name = (f.FieldName ?? "").ToLowerInvariant();
					var op = (f.Operator ?? "").ToLowerInvariant();
					var hasVJ = !string.IsNullOrWhiteSpace(f.ValueJson);
					var hasDV = !string.IsNullOrWhiteSpace(f.DefaultValue);

					// 1) 日期 + 粒度
					if (name is "orderdate" or "borrowdate" or "daterange")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<DateRangeV>(f.ValueJson!)!;
							if (DateTime.TryParse(v.from, out var d1)) dateFrom = d1.Date;
							if (DateTime.TryParse(v.to, out var d2)) dateTo = d2.Date.AddDays(1).AddTicks(-1);
							var g = (v.gran ?? "day").ToLowerInvariant();
							if (g is "day" or "month" or "year") gran = g;
						}
						else if (hasDV)
						{
							// 舊格式：yyyy-MM-dd~yyyy-MM-dd;gran=day
							var tokens = f.DefaultValue!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
							// gran
							foreach (var t in tokens)
							{
								var kv = t.Split('=', 2, StringSplitOptions.TrimEntries);
								if (kv.Length == 2 && kv[0].Equals("gran", StringComparison.OrdinalIgnoreCase))
								{
									var g = kv[1].ToLowerInvariant();
									if (g is "day" or "month" or "year") gran = g;
								}
							}
							// range
							var rangePart = tokens.FirstOrDefault(x => x.Contains('~')) ?? f.DefaultValue!;
							var rp = rangePart.Split('~', StringSplitOptions.TrimEntries);
							if (rp.Length >= 1 && DateTime.TryParse(rp[0], out var d1)) dateFrom = d1.Date;
							if (rp.Length >= 2)
							{
								var right = rp[1].Split('=', 2)[0]; // 移除可能殘留的 "=xxx"
								if (DateTime.TryParse(right, out var d2)) dateTo = d2.Date.AddDays(1).AddTicks(-1);
							}
						}
					}
					// 2) 類別（多選）
					else if (name == "categoryid" && op == "in")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<InV>(f.ValueJson!)!;
							if (v.values?.Any() == true) categoryIds = v.values!.ToList();
						}
						else if (hasDV)
						{
							// 空字串代表「全部」→ 忽略此條件
							var tmp = f.DefaultValue!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
													 .Select(s => int.TryParse(s, out var x) ? x : (int?)null)
													 .Where(x => x.HasValue).Select(x => x!.Value).ToList();
							if (tmp.Any()) categoryIds = tmp;
						}
					}
					// 3) 價位（between）
					else if (name == "saleprice" && op == "between")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<RangeV>(f.ValueJson!)!;
							if (v.min.HasValue) priceMin = (int)v.min.Value;
							if (v.max.HasValue) priceMax = (int)v.max.Value;
						}
						else if (hasDV)
						{
							var p = f.DefaultValue!.Split('-', 2, StringSplitOptions.TrimEntries);
							if (int.TryParse(p.ElementAtOrDefault(0), out var a)) priceMin = a;
							if (int.TryParse(p.ElementAtOrDefault(1), out var b)) priceMax = b;
						}
					}
					// 4) 排行（between，保留相容）
					else if (name == "rank" && op == "between")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<RangeV>(f.ValueJson!)!;
							if (v.max.HasValue) topTo = Math.Clamp((int)v.max.Value, 1, 50);
						}
						else if (hasDV)
						{
							var p = f.DefaultValue!.Split('-', 2, StringSplitOptions.TrimEntries);
							if (int.TryParse(p.ElementAtOrDefault(1), out var t)) topTo = Math.Clamp(t, 1, 50);
						}
					}
					// 5) 訂單指標 amount|count
					else if (name == "metric")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<ValueV>(f.ValueJson!)!;
							if (!string.IsNullOrWhiteSpace(v.value)) orderMetric = v.value!.ToLowerInvariant() == "count" ? "count" : "amount";
						}
						else if (hasDV)
						{
							orderMetric = f.DefaultValue!.ToLowerInvariant() == "count" ? "count" : "amount";
						}
					}
					// 6) 訂單狀態（新：包含式）
					else if (name == "orderstatus" && op == "in")
					{
						statusSpecifiedByInclude = true;
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<InV>(f.ValueJson!)!;
							inclStatus = (v.values ?? Array.Empty<int>()).ToList(); // 可能為空（代表全不勾）
						}
						else if (hasDV)
						{
							inclStatus = f.DefaultValue!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
														 .Select(s => int.TryParse(s, out var x) ? x : (int?)null)
														 .Where(x => x.HasValue).Select(x => x!.Value).ToList();
						}
					}
					// 7) 舊：排除狀態（相容）
					else if (name == "excludestatus" && op == "in" && hasDV)
					{
						exclStatus = f.DefaultValue!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
													.Select(s => int.TryParse(s, out var x) ? x : (int?)null)
													.Where(x => x.HasValue).Select(x => x!.Value).ToList();
					}
					// 8) 單筆訂單金額（between）
					else if (name == "orderamount" && op == "between")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<RangeV>(f.ValueJson!)!;
							orderAmtMin = v.min; orderAmtMax = v.max;
						}
						else if (hasDV)
						{
							var p = f.DefaultValue!.Split('-', 2, StringSplitOptions.TrimEntries);
							if (decimal.TryParse(p.ElementAtOrDefault(0), out var a)) orderAmtMin = a;
							if (decimal.TryParse(p.ElementAtOrDefault(1), out var b)) orderAmtMax = b;
						}
					}
					// 9) 排行區間（RankRange，兩下拉）— 允許 "1-10" 或 JSON {"from":1,"to":10}
					else if (name == "rankrange")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<RankRangeV>(f.ValueJson!)!;
							if (v.from.HasValue) rankFrom = Math.Max(1, v.from.Value);
							if (v.to.HasValue) rankTo = Math.Max(rankFrom, Math.Min(50, v.to.Value));
						}
						else if (hasDV)
						{
							var p = f.DefaultValue!.Split('-', 2, StringSplitOptions.TrimEntries);
							if (int.TryParse(p.ElementAtOrDefault(0), out var f1)) rankFrom = Math.Max(1, f1);
							if (int.TryParse(p.ElementAtOrDefault(1), out var t1)) rankTo = Math.Max(rankFrom, Math.Min(50, t1));
						}
					}

					// 10) 預留-出版年份（十年區間，借閱用）— 允許 "1991-2000" 或 JSON {"fromYear":1991,"toYear":2000}
					else if (name == "publishdecade")
					{
						if (hasVJ)
						{
							var v = JsonSerializer.Deserialize<DecadeV>(f.ValueJson!)!;
							decadeFrom = v.fromYear; decadeTo = v.toYear;
						}
						else if (hasDV)
						{
							var p = f.DefaultValue!.Split('-', 2, StringSplitOptions.TrimEntries);
							if (int.TryParse(p.ElementAtOrDefault(0), out var fy)) decadeFrom = fy;
							if (int.TryParse(p.ElementAtOrDefault(1), out var ty)) decadeTo = ty;
						}
					}

				}

				// 預設日期 & 端點修正
				if (dateFrom == default) dateFrom = DateTime.Today.AddDays(-29);
				if (dateTo == default) dateTo = DateTime.Today;
				// 右邊補到當天 23:59:59.9999999
				dateTo = dateTo.Date.AddDays(1).AddTicks(-1);
				// 使用者若把 from/to 反了，幫他交換
				if (dateFrom > dateTo) (dateFrom, dateTo) = (dateTo, dateFrom);

				// ★ 單位字串（for 標題）
				var unit = gran switch { "month" => "月", "year" => "年", _ => "日" };

				// ★ 追蹤：跑到 server console（Kestrel 輸出）
				Console.WriteLine($"[PreviewDraft] cat={cat}, baseKind={baseKind}, gran={gran}, range={dateFrom:yyyy-MM-dd}~{dateTo:yyyy-MM-dd}");

				// ★ 也帶回給前端（不影響前端繪圖；僅方便除錯）
				var echo = new
				{
					cat,
					baseKind,
					gran,
					dateFrom = dateFrom.ToString("yyyy-MM-dd"),
					dateTo = dateTo.ToString("yyyy-MM-dd"),
					filters = req?.Filters?.Select(f => new { f.FieldName, f.DataType, f.Operator, f.ValueJson })
				};

				// =========================
				//   Bar / Pie  — TopN
				// =========================
				if (cat is "bar" or "pie")
				{
					if (baseKind == "sales")
					{
						// === sales：以「書(BookID)」為單位，在日期+種類下統計銷售本數 ===
						{
							var q = from od in _shop.OrderDetails.AsNoTracking()
									join o in _shop.Orders.AsNoTracking() on od.OrderID equals o.OrderID
									join b in _shop.Books.AsNoTracking() on od.BookID equals b.BookID
									join p in _shop.Publishers.AsNoTracking() on b.PublisherID equals p.PublisherID // ⬅ 新增
									where o.OrderDate >= dateFrom && o.OrderDate <= dateTo && o.Status != 0
									select new { o.OrderDate, b.CategoryID, b.BookID, Title = b.Title, Qty = od.Quantity, SupplierID = p.SupplierID }; // ⬅ 帶出 SupplierID

							// 資料範圍
							if (!canAll && canPub)
							{
								var supplierId = GetSupplierIdOrThrow();
								q = q.Where(x => x.SupplierID == supplierId);
							}


							if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));
							if (priceMin.HasValue || priceMax.HasValue)
							{
								var priced = from x in q
											 join b in _shop.Books.AsNoTracking() on x.BookID equals b.BookID
											 let Price = (b.SalePrice ?? b.ListPrice)
											 where (!priceMin.HasValue || Price >= priceMin.Value)
												&& (!priceMax.HasValue || Price <= priceMax.Value)
											 select new { x.BookID, x.Title, x.Qty, x.SupplierID };
								q = from x in priced select new { OrderDate = dateFrom, CategoryID = 0, x.BookID, x.Title, Qty = x.Qty, x.SupplierID
                                };
							}

							var grouped = await q.GroupBy(x => new { x.BookID, x.Title })
												 .Select(g => new { g.Key.BookID, Name = g.Key.Title, V = g.Sum(x => (decimal)x.Qty) })
												 .OrderByDescending(x => x.V)
												 .ToListAsync();

							var from = Math.Max(1, rankFrom);
							var to = Math.Max(from, rankTo);
							var slice = grouped.Skip(from - 1).Take(to - from + 1)
											   .Select(x => new { label = x.Name, value = x.V })
											   .ToList();

							return Json(new
							{
								ok = true,
								title = $"書籍銷售本數",
								echo = new { category = cat, baseKind, date = new { from = dateFrom, to = dateTo, gran }, categoryIds, rank = new { from, to } },
								series = slice
							});
						}
					}
					else if (baseKind == "borrow")
					{
						// === borrow：以「上架(ListingID)」為單位，在日期+種類下統計借閱次數 ===
						{
							var q = from br in _shop.BorrowRecords.AsNoTracking()
									join l in _shop.Listings.AsNoTracking() on br.ListingID equals l.ListingID
									join p in _shop.Publishers.AsNoTracking() on l.PublisherID equals p.PublisherID // ⬅ 新增
									where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
									select new { br.BorrowDate, l.CategoryID, l.ListingID, Name = l.Title, SupplierID = p.SupplierID }; // ⬅ 帶出 SupplierID

							if (!canAll && canPub)
							{
								var supplierId = GetSupplierIdOrThrow();
								q = q.Where(x => x.SupplierID == supplierId);
							}


							if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));

							var grouped = await q.GroupBy(x => new { x.ListingID, x.Name })
												 .Select(g => new { g.Key.ListingID, Name = g.Key.Name, V = g.Count() })
												 .OrderByDescending(x => x.V)
												 .ToListAsync();

							var from = Math.Max(1, rankFrom);
							var to = Math.Max(from, rankTo);
							var slice = grouped.Skip(from - 1).Take(to - from + 1)
											   .Select(x => new { label = x.Name, value = (decimal)x.V })
											   .ToList();

							return Json(new
							{
								ok = true,
								title = $"書籍借閱本數",
								echo = new { category = cat, baseKind, date = new { from = dateFrom, to = dateTo, gran }, categoryIds, rank = new { from, to } },
								series = slice
							});
						}

					}
					else
					{
						// orders 不支援 bar/pie
						return Json(new { ok = false, error = "orders 不支援 bar/pie", echo });
					}
				}

				// =========================
				//   Line
				// =========================
				// ===== 分支：sales / borrow / orders =====
				if (baseKind == "sales")
				{
					var q = from od in _shop.OrderDetails
							join o in _shop.Orders on od.OrderID equals o.OrderID
							join b in _shop.Books on od.BookID equals b.BookID
							join p in _shop.Publishers on b.PublisherID equals p.PublisherID // ⬅ 新增
							where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
							select new
							{
								o.OrderDate,
								b.CategoryID,
								Price = (b.SalePrice ?? b.ListPrice),
								od.Quantity,
								SupplierID = p.SupplierID // ⬅ 帶出 SupplierID
							};

					if (!canAll && canPub)
					{
						var supplierId = GetSupplierIdOrThrow();
						q = q.Where(x => x.SupplierID == supplierId);
					}


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
						return Json(new { title = $"每{unit}銷售本數", labels, data, echo });
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
						return Json(new { title = $"每{unit}銷售本數", labels, data, echo });
					}
					else
					{
						var raw = await q.GroupBy(x => x.OrderDate.Date)
										 .Select(g => new { Day = g.Key, V = g.Sum(x => (decimal)x.Quantity) })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => k.Day, v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var d in EachDay(dateFrom, dateTo))
						{
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue(d.Date, out var v) ? v : 0m);
						}
						return Json(new { title = $"每{unit}銷售本數", labels, data, echo });
					}
				}
				else if (baseKind == "borrow")
				{
					var q = from br in _shop.BorrowRecords
							join l in _shop.Listings on br.ListingID equals l.ListingID
							join p in _shop.Publishers on l.PublisherID equals p.PublisherID // ⬅ 新增
							where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
							select new { br.BorrowDate, l.CategoryID, SupplierID = p.SupplierID }; // ⬅ 帶出 SupplierID

					if (!canAll && canPub)
					{
						var supplierId = GetSupplierIdOrThrow();
						q = q.Where(x => x.SupplierID == supplierId);
					}


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
						return Json(new { title = $"每{unit}借閱本數", labels, data, echo });
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
						return Json(new { title = $"每{unit}借閱本數", labels, data, echo });
					}
					else
					{
						var raw = await q.GroupBy(x => x.BorrowDate.Date)
										 .Select(g => new { Day = g.Key, V = g.Count() })
										 .ToListAsync();

						var dict = raw.ToDictionary(k => k.Day, v => (decimal)v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var d in EachDay(dateFrom, dateTo))
						{
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue(d.Date, out var v) ? v : 0m);
						}
						return Json(new { title = $"每{unit}借閱本數", labels, data, echo });
					}
				}
				else // orders
				{
					// 先把 Orders ↔ OrderDetails ↔ Books ↔ Publishers 串起來，拿到 p.SupplierID
					var q = from o in _shop.Orders.AsNoTracking()
							join od in _shop.OrderDetails.AsNoTracking() on o.OrderID equals od.OrderID
							join b in _shop.Books.AsNoTracking() on od.BookID equals b.BookID
							join p in _shop.Publishers.AsNoTracking() on b.PublisherID equals p.PublisherID
							where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
							select new
							{
								o.OrderID,
								o.OrderDate,
								o.TotalAmount,
								Status = (int)o.Status,
								SupplierID = p.SupplierID
							};

					// ★ 資料範圍（書商）
					if (!canAll && canPub)
					{
						var supplierId = GetSupplierIdOrThrow();
						q = q.Where(x => x.SupplierID == supplierId);
					}

					// ★ 訂單狀態（與你原本一致：包含式優先、再來排除式）
					if (statusSpecifiedByInclude)
					{
						if (inclStatus.Any())
							q = q.Where(x => inclStatus.Contains(x.Status));
						else
							q = q.Where(x => false); // 全不勾 → 無結果
					}
					else if (exclStatus.Any())
					{
						q = q.Where(x => !exclStatus.Contains(x.Status));
					}

					// ★ 訂單金額 between
					if (orderAmtMin.HasValue) q = q.Where(x => x.TotalAmount >= orderAmtMin.Value);
					if (orderAmtMax.HasValue) q = q.Where(x => x.TotalAmount <= orderAmtMax.Value);

					// 由於我們 join 了明細，為了維持「你的原意：以『訂單』為單位」
					// 這裡把同一張訂單聚成一筆（避免一張多列 OrderDetails 被重複計數）
					var qo = q.GroupBy(x => new { x.OrderID, x.OrderDate, x.TotalAmount })
							  .Select(g => new { g.Key.OrderID, g.Key.OrderDate, g.Key.TotalAmount });

					var titlePrefix = orderMetric == "count" ? "總銷售筆數" : "總銷售金額";

					if (gran == "month")
					{
						List<YMVal> raw;
						if (orderMetric == "count")
						{
							raw = await qo.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
										  .Select(g => new YMVal { Year = g.Key.Year, Month = g.Key.Month, V = (decimal)g.Count() })
										  .ToListAsync();
						}
						else
						{
							raw = await qo.GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
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
						return Json(new { title = $"每{unit}{titlePrefix}", labels, data, echo });
					}
					else if (gran == "year")
					{
						List<YVal> raw;
						if (orderMetric == "count")
						{
							raw = await qo.GroupBy(x => x.OrderDate.Year)
										  .Select(g => new YVal { Year = g.Key, V = (decimal)g.Count() })
										  .ToListAsync();
						}
						else
						{
							raw = await qo.GroupBy(x => x.OrderDate.Year)
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
						return Json(new { title = $"每{unit}{titlePrefix}", labels, data, echo });
					}
					else
					{
						List<DVal> raw;
						if (orderMetric == "count")
						{
							raw = await qo.GroupBy(x => x.OrderDate.Date)
										  .Select(g => new DVal { Day = g.Key, V = (decimal)g.Count() })
										  .ToListAsync();
						}
						else
						{
							raw = await qo.GroupBy(x => x.OrderDate.Date)
										  .Select(g => new DVal { Day = g.Key, V = g.Sum(x => x.TotalAmount) })
										  .ToListAsync();
						}

						var dict = raw.ToDictionary(k => k.Day, v => v.V);
						var labels = new List<string>();
						var data = new List<decimal>();
						foreach (var d in EachDay(dateFrom, dateTo))
						{
							labels.Add(d.ToString("yyyy-MM-dd"));
							data.Add(dict.TryGetValue(d.Date, out var v) ? v : 0m);
						}
						return Json(new { title = $"每{unit}{titlePrefix}", labels, data, echo });
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
