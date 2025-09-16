using Microsoft.AspNetCore.Authorization;
﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Shop;
using System.Text.Json;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Authorize(Roles = "Admin,Marketing,Publisher")]
    [Route("ReportMail/[controller]/[action]")]
    public class ReportPreviewController : Controller
    {
        private readonly ShopDbContext _shop;
        private readonly IPublisherScopeService _scope;
        public ReportPreviewController(ShopDbContext shop, IPublisherScopeService scope)
        {
            _shop = shop;
            _scope = scope;
        }

        
        // ===== Publisher 可視範圍與 UI 篩選的交集 =====
        private static int[] ParsePublisherIdsFromFilters(List<FilterDraft>? filters)
        {
            if (filters == null) return Array.Empty<int>();
            foreach (var f in filters)
            {
                var name = (f.FieldName ?? "").Trim().ToLowerInvariant();
                if (name == "publisherid" || name == "publisherids" || name == "publisher")
                {
                    if (!string.IsNullOrWhiteSpace(f.ValueJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(f.ValueJson);
                            return doc.RootElement.EnumerateArray()
                                      .Select(e => e.TryGetInt32(out var n) ? n : (int?)null)
                                      .Where(n => n.HasValue).Select(n => n!.Value)
                                      .Distinct().ToArray();
                        }
                        catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(f.DefaultValue))
                    {
                        var arr = f.DefaultValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                                .Where(n => n.HasValue).Select(n => n!.Value)
                                .Distinct().ToArray();
                        return arr;
                    }
                }
            }
            return Array.Empty<int>();
        }

        private (bool limit, int[] ids) ResolveEffectivePublisherScope(PreviewReq dto)
        {
            // Admin/Marketing：不限制
            if (_scope.IsAdminOrMarketing(User)) return (false, Array.Empty<int>());

            // 一般員工（非書商）→ 直接無資料
            if (!_scope.IsPublisher(User)) return (true, Array.Empty<int>());

            // 書商： claims/對映表給的可視範圍
            var allowed = _scope.GetPublisherIds(User) ?? Array.Empty<int>();
            if (allowed.Length == 0) return (true, Array.Empty<int>());

            // 與 UI 傳來的 Publisher 篩選取交集（若 UI 沒帶，就用 allowed）
            var ui = ParsePublisherIdsFromFilters(dto.Filters);
            if (ui.Length == 0) return (true, allowed.Distinct().ToArray());
            var set = ui.Intersect(allowed).Distinct().ToArray();
            return (true, set);
        }
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
        public async Task<IActionResult> PreviewDraft([FromBody] PreviewReq req)
        {
            try
            {
                // ★ 強制套用出版社可視範圍（Admin/Marketing 不限制；一般員工回空；書商取交集）
                var (limitByPublisher, effPubIds) = ResolveEffectivePublisherScope(req);

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
						// 來源：訂單明細 × 訂單 × 書籍
						var q = from od in _shop.OrderDetails.AsNoTracking()
								join o in _shop.Orders.AsNoTracking() on od.OrderID equals o.OrderID
								join b in _shop.Books.AsNoTracking() on od.BookID equals b.BookID
								where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
								select new
								{
									b.BookID,
									b.Title,
									b.CategoryID,
									Price = (b.SalePrice ?? b.ListPrice),
									od.Quantity
								};

						if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));
						if (priceMin.HasValue) q = q.Where(x => x.Price >= priceMin.Value);
						if (priceMax.HasValue) q = q.Where(x => x.Price <= priceMax.Value);

						// TopN（以銷售「本數」排序）
						var grouped = await q.GroupBy(x => new { x.BookID, x.Title })
											 .Select(g => new { g.Key.BookID, g.Key.Title, V = g.Sum(x => (decimal)x.Quantity) })
											 .OrderByDescending(x => x.V)
											 .ToListAsync();

						var from = Math.Max(1, rankFrom);
						var to = Math.Max(from, rankTo);
						var slice = grouped.Skip(from - 1).Take(to - from + 1)
										   .Select(x => new { label = x.Title, value = x.V })
										   .ToList();

						return Json(new
						{
							ok = true,
							title = $"書籍銷售本數",
							echo = new { category = cat, baseKind, date = new { from = dateFrom, to = dateTo, gran }, categoryIds, price = new { min = priceMin, max = priceMax }, rank = new { from, to } },
							series = slice
						});
					}
					else if (baseKind == "borrow")
					{
						// 來源：借閱紀錄 × Listing（有分類/書名）；若你的資料有出版年，可再 join 書籍
						var q = from br in _shop.BorrowRecords.AsNoTracking()
								join l in _shop.Listings.AsNoTracking() on br.ListingID equals l.ListingID
								where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
								select new { br.BorrowDate, l.CategoryID, l.Title, l.ISBN, l.PublisherID };
                        if (limitByPublisher) q = q.Where(x => effPubIds.Contains(x.PublisherID));

						if (categoryIds.Any()) q = q.Where(x => categoryIds.Contains(x.CategoryID));

						// 預留借閱書籍出版年，可把下段註解改為實作：
						// join b in _shop.Books on x.ISBN equals b.ISBN
						// where (!decadeFrom.HasValue || b.PublishYear >= decadeFrom.Value)
						//    && (!decadeTo.HasValue   || b.PublishYear <= decadeTo.Value)

						var grouped = await q.GroupBy(x => x.Title)
											 .Select(g => new { Title = g.Key, V = g.Count() })
											 .OrderByDescending(x => x.V)
											 .ToListAsync();

						var from = Math.Max(1, rankFrom);
						var to = Math.Max(from, rankTo);
						var slice = grouped.Skip(from - 1).Take(to - from + 1)
										   .Select(x => new { label = x.Title, value = (decimal)x.V })
										   .ToList();

						return Json(new
						{
							ok = true,
							title = $"書籍借閱本數",
							echo = new { category = cat, baseKind, date = new { from = dateFrom, to = dateTo, gran }, categoryIds, decade = new { fromYear = decadeFrom, toYear = decadeTo }, rank = new { from, to } },
							series = slice
						});
					}
					else
					{
						// orders 不支援 bar/pie
						return Json(new { ok = false, error = "orders 不支援 bar/pie", echo });
					}
				}


				// ===== 分支：sales / borrow / orders =====
				if (baseKind == "sales")
                {
                    var q = from od in _shop.OrderDetails
                            join o in _shop.Orders on od.OrderID equals o.OrderID
                            join b in _shop.Books on od.BookID equals b.BookID
                            where o.OrderDate >= dateFrom && o.OrderDate <= dateTo
                            select new { o.OrderDate, b.CategoryID, b.PublisherID, Price = (b.SalePrice ?? b.ListPrice), od.Quantity };
                    if (limitByPublisher) q = q.Where(x => effPubIds.Contains(x.PublisherID));

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
                            where br.BorrowDate >= dateFrom && br.BorrowDate <= dateTo
                            select new { br.BorrowDate, l.CategoryID };

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
                    var q = _shop.Orders.AsNoTracking()
                                .Where(o => o.OrderDate >= dateFrom && o.OrderDate <= dateTo);
                    if (limitByPublisher)
                    {
                        q = (from o in q
                             join od in _shop.OrderDetails.AsNoTracking() on o.OrderID equals od.OrderID
                             join b in _shop.Books.AsNoTracking() on od.BookID equals b.BookID
                             where effPubIds.Contains(b.PublisherID)
                             select o).Distinct();
                    }

                    // 新：包含式（若前端有送 OrderStatus，優先用）
                    if (statusSpecifiedByInclude)
                    {
                        if (inclStatus.Any())
                            q = q.Where(o => inclStatus.Contains((int)o.Status));
                        else
                            q = q.Where(o => 1 == 0); // 全不勾 → 無結果
                    }
                    // 舊：排除式（僅在前端未送包含式時相容）
                    else if (exclStatus.Any())
                    {
                        q = q.Where(o => !exclStatus.Contains((int)o.Status));
                    }

                    if (orderAmtMin.HasValue) q = q.Where(o => o.TotalAmount >= orderAmtMin.Value);
                    if (orderAmtMax.HasValue) q = q.Where(o => o.TotalAmount <= orderAmtMax.Value);

                    var titlePrefix = orderMetric == "count" ? "總銷售筆數" : "總銷售金額";

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
                        return Json(new { title = $"每{unit}{titlePrefix}", labels, data, echo });
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
                        return Json(new { title = $"每{unit}{titlePrefix}", labels, data, echo });
                    }
                    else
                    {
                        List<DVal> raw;
                        if (orderMetric == "count")
                        {
                            raw = await q.GroupBy(o => o.OrderDate.Date)
                                         .Select(g => new DVal { Day = g.Key, V = (decimal)g.Count() })
                                         .ToListAsync();
                        }
                        else
                        {
                            raw = await q.GroupBy(o => o.OrderDate.Date)
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
