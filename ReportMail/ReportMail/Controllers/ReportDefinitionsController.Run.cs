using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReportMail.Models;

namespace ReportMail.Controllers
{
    public partial class ReportDefinitionsController : Controller
    {
        // 生成 CSV（UTF-8 with BOM）
        private static byte[] BuildCsv(string reportName, List<SubmittedFilter> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ReportName,{Escape(reportName)}");
            sb.AppendLine($"GeneratedAt,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("FieldName,DisplayName,DataType,Operator,Value");
            foreach (var x in items)
                sb.AppendLine($"{Escape(x.FieldName)},{Escape(x.DisplayName)},{Escape(x.DataType)},{Escape(x.Operator)},{Escape(x.Value)}");

            return System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            static string Escape(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                var needQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n');
                return needQuote ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
            }
        }

        // 產生檔名與路徑（/wwwroot/exports/Report_yyyyMMdd_HHmmss.csv）
        private static (string fileName, string relativePath, string physicalPath)
            GetExportPaths(string reportName, string ext, string webRoot)
        {
            string slug = string.Concat(reportName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
            if (string.IsNullOrWhiteSpace(slug)) slug = "Report";
            var fileName = $"{slug}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
            var relative = Path.Combine("exports", fileName);
            var physical = Path.Combine(webRoot, relative);
            return (fileName, relative, physical);
        }

        [HttpGet]
        public async Task<IActionResult> DebugDb()
        {
            var conn = _context.Database.GetDbConnection().ConnectionString;
            var defs = await _context.ReportDefinitions.CountAsync();
            var fil = await _context.ReportFilters.CountAsync();
            return Content($"Conn={conn}\nReportDefinition={defs}\nReportFilter={fil}");
        }

        // GET: /ReportDefinitions/Run/5
        // 顯示「動態條件表單」：依 ReportFilter 的型別/運算子產生輸入欄位
        public async Task<IActionResult> Run(int id)
        {
            // 只載入「啟用中的」篩選欄位
            var def = await _context.ReportDefinitions
                .Include(d => d.ReportFilters.Where(f => f.IsActive))
                .FirstOrDefaultAsync(d => d.ReportDefinitionId == id);

            if (def == null) return NotFound();

            // ★ 寫入 ReportAccessLog：開啟執行頁（條件表單）
            _context.ReportAccessLogs.Add(new ReportAccessLog
            {
                UserId = null,  // 有身分系統再填實際使用者 Id
                ReportDefinitionId = def.ReportDefinitionId,
                AccessedAt = DateTime.Now,
                ActionType = "open", // 自定義：open=開啟條件頁
                Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });
            await _context.SaveChangesAsync();

            // 解析每個欄位的 Options → 下拉選單（select/multiselect/in 才需要）
            var selectDict = new Dictionary<int, SelectList>();
            foreach (var f in def.ReportFilters.OrderBy(x => x.OrderIndex))
            {
                if (NeedsSelect(f))
                    selectDict[f.ReportFilterId] = BuildSelectListFromOptions(f.Options);
            }

            ViewBag.Selects = selectDict;        // 給 View 用
            ViewBag.ReportName = def.ReportName; // 顯示用

            return View(def); // 傳入 ReportDefinition（含 ReportFilters）
        }

        // POST: /ReportDefinitions/Run/5
        // 接收表單 → 依 IsRequired 做伺服器端必填驗證 → 顯示條件摘要
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run(int id, IFormCollection form)
        {
            var def = await _context.ReportDefinitions
                .Include(d => d.ReportFilters.Where(f => f.IsActive))
                .FirstOrDefaultAsync(d => d.ReportDefinitionId == id);

            if (def == null) return NotFound();

            var results = new List<SubmittedFilter>();

            foreach (var f in def.ReportFilters.OrderBy(x => x.OrderIndex))
            {
                var key = $"f_{f.ReportFilterId}";    // 主輸入欄位 name
                var keyTo = $"f_{f.ReportFilterId}_to"; // between 第二格 name

                var values = form[key];   // 單值或多值
                var valueTo = form[keyTo]; // 只有 between 會有

                // 必填檢查（依型態分三種）
                var emptySingle = values.Count == 0 || string.IsNullOrWhiteSpace(values.FirstOrDefault());
                var emptyMulti = values.Count == 0;
                var emptyBetween = string.IsNullOrWhiteSpace(values.FirstOrDefault()) ||
                                   string.IsNullOrWhiteSpace(valueTo.FirstOrDefault());

                if (f.IsRequired)
                {
                    if (IsBetween(f) && emptyBetween)
                        ModelState.AddModelError(key, $"{f.DisplayName} 為必填（需兩個值）");
                    else if (IsMulti(f) && emptyMulti)
                        ModelState.AddModelError(key, $"{f.DisplayName} 為必填（至少選一個）");
                    else if (!IsBetween(f) && !IsMulti(f) && emptySingle)
                        ModelState.AddModelError(key, $"{f.DisplayName} 為必填");
                }

                // 組顯示值（摘要用）
                string displayValue =
                    IsBetween(f) ? $"{values.FirstOrDefault()} ~ {valueTo.FirstOrDefault()}" :
                    IsMulti(f) ? string.Join(", ", values.ToArray()) :
                                    values.FirstOrDefault();

                results.Add(new SubmittedFilter
                {
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    DataType = f.DataType,
                    Operator = f.Operator,
                    Value = displayValue
                });
            }

            if (!ModelState.IsValid)
            {
                // 驗證失敗：把下拉選項補回去，回同一個 View 顯示錯誤
                var selectDict = new Dictionary<int, SelectList>();
                foreach (var f in def.ReportFilters)
                    if (NeedsSelect(f)) selectDict[f.ReportFilterId] = BuildSelectListFromOptions(f.Options);

                ViewBag.Selects = selectDict;
                ViewBag.ReportName = def.ReportName;
                return View(def);
            }

            // 依按下的按鈕分流（Run.cshtml 有 name="__action" 的兩顆按鈕）
            var action = form["__action"].ToString();

            if (string.Equals(action, "export", StringComparison.OrdinalIgnoreCase))
            {
                // 1) 將條件整理成簡單字典（寫入 Log.Filters）
                var filtersDict = results.ToDictionary(x => x.FieldName, x => x.Value ?? "");
                var filtersJson = System.Text.Json.JsonSerializer.Serialize(filtersDict);

                // 2) 產出 CSV 內容（用下面 BuildCsv）
                var csvBytes = BuildCsv(def.ReportName, results);

                // 3) 存檔到 wwwroot/exports（用下面 GetExportPaths）
                var (fileName, relativePath, physicalPath) = GetExportPaths(def.ReportName, "csv", _env.WebRootPath);
                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
                System.IO.File.WriteAllBytes(physicalPath, csvBytes);

                // 4) 寫 ReportExportLog（只用你資料表有的欄位）
                _context.ReportExportLogs.Add(new ReportExportLog
                {
                    UserId = null, // 有登入再填
                    ReportName = def.ReportName,
                    ExportFormat = "CSV",
                    ExportAt = DateTime.Now,
                    Filters = filtersJson,
                    FilePath = "/" + relativePath.Replace('\\', '/')
                });

                // 5) 寫 ReportAccessLog：匯出動作
                _context.ReportAccessLogs.Add(new ReportAccessLog
                {
                    UserId = null,
                    ReportDefinitionId = def.ReportDefinitionId,
                    AccessedAt = DateTime.Now,
                    ActionType = "export",
                    Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                });

                await _context.SaveChangesAsync();

                // 6) 直接下載檔案
                return File(csvBytes, "text/csv", fileName);
            }

            // 沒選匯出就回到「條件摘要」頁（保留你原本的邏輯）


            // ✅ 驗證通過：先回「條件摘要」頁；之後可在這改為匯出或查詢
            return View("RunResult", new RunResultVM
            {
                ReportDefinitionId = def.ReportDefinitionId,
                ReportName = def.ReportName,
                Items = results
            });
        }

        // ===== Helpers（僅這支檔案使用）=====

        private static bool NeedsSelect(ReportFilter f) =>
            f.DataType.Equals("select", StringComparison.OrdinalIgnoreCase) ||
            f.DataType.Equals("multiselect", StringComparison.OrdinalIgnoreCase) ||
            f.Operator.Equals("in", StringComparison.OrdinalIgnoreCase);

        private static bool IsBetween(ReportFilter f) =>
            f.Operator.Equals("between", StringComparison.OrdinalIgnoreCase);

        private static bool IsMulti(ReportFilter f) =>
            f.DataType.Equals("multiselect", StringComparison.OrdinalIgnoreCase) ||
            f.Operator.Equals("in", StringComparison.OrdinalIgnoreCase);

        // Options 支援：
        // 1) ["A","B"]  2) [{"value":"A","text":"選項A"}]  3) "A,B,C"
        private static SelectList BuildSelectListFromOptions(string? options)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(options))
                    return new SelectList(Array.Empty<SelectListItem>());

                var obj = JsonSerializer.Deserialize<List<SimpleOption>>(options);
                if (obj != null && obj.Count > 0)
                    return new SelectList(obj, nameof(SimpleOption.Value), nameof(SimpleOption.Text));

                var arr = JsonSerializer.Deserialize<List<string>>(options) ?? new List<string>();
                return new SelectList(arr.Select(x => new SelectListItem(x, x)), "Value", "Text");
            }
            catch
            {
                var parts = options!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new SelectList(parts.Select(x => new SelectListItem(x, x)), "Value", "Text");
            }
        }

        private record SimpleOption(string Value, string Text);

        // 顯示摘要用的小型 ViewModel（不會寫 DB）
        public class SubmittedFilter
        {
            public string FieldName { get; set; } = null!;
            public string DisplayName { get; set; } = null!;
            public string DataType { get; set; } = null!;
            public string Operator { get; set; } = null!;
            public string? Value { get; set; }
        }

        public class RunResultVM
        {
            public int ReportDefinitionId { get; set; }
            public string ReportName { get; set; } = null!;
            public List<SubmittedFilter> Items { get; set; } = new();
        }
        // ==================== Charts ====================

        // GET: /ReportDefinitions/Chart/5
        public async Task<IActionResult> Chart(int id)
        {
            var def = await _context.ReportDefinitions
                .Include(d => d.ReportFilters.Where(f => f.IsActive))
                .FirstOrDefaultAsync(d => d.ReportDefinitionId == id);
            if (def == null) return NotFound();

            // 寫入開啟圖表頁的 access log（可選）
            _context.ReportAccessLogs.Add(new ReportAccessLog
            {
                UserId = null,
                ReportDefinitionId = def.ReportDefinitionId,
                AccessedAt = DateTime.Now,
                ActionType = "chart-open",
                Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });
            await _context.SaveChangesAsync();

            // 下拉選單資料
            var selectDict = new Dictionary<int, SelectList>();
            foreach (var f in def.ReportFilters.OrderBy(x => x.OrderIndex))
                if (NeedsSelect(f)) selectDict[f.ReportFilterId] = BuildSelectListFromOptions(f.Options);
            ViewBag.Selects = selectDict;
            ViewBag.ReportName = def.ReportName;

            return View(def); // 直接用 ReportDefinition（含 ReportFilters）去長表單
        }

        // POST: /ReportDefinitions/Chart/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Chart(int id, IFormCollection form)
        {
            var def = await _context.ReportDefinitions
                .Include(d => d.ReportFilters.Where(f => f.IsActive))
                .FirstOrDefaultAsync(d => d.ReportDefinitionId == id);
            if (def == null) return NotFound();

            // 解析條件（沿用 Run 的邏輯）
            var results = new List<SubmittedFilter>();
            foreach (var f in def.ReportFilters.OrderBy(x => x.OrderIndex))
            {
                var key = $"f_{f.ReportFilterId}";
                var keyTo = $"f_{f.ReportFilterId}_to";
                var values = form[key];
                var valueTo = form[keyTo];

                var emptySingle = values.Count == 0 || string.IsNullOrWhiteSpace(values.FirstOrDefault());
                var emptyMulti = values.Count == 0;
                var emptyBetween = string.IsNullOrWhiteSpace(values.FirstOrDefault()) || string.IsNullOrWhiteSpace(valueTo.FirstOrDefault());

                if (f.IsRequired)
                {
                    if (IsBetween(f) && emptyBetween) ModelState.AddModelError(key, $"{f.DisplayName} 為必填（需兩個值）");
                    else if (IsMulti(f) && emptyMulti) ModelState.AddModelError(key, $"{f.DisplayName} 為必填（至少選一個）");
                    else if (!IsBetween(f) && !IsMulti(f) && emptySingle) ModelState.AddModelError(key, $"{f.DisplayName} 為必填");
                }

                string displayValue =
                    IsBetween(f) ? $"{values.FirstOrDefault()} ~ {valueTo.FirstOrDefault()}" :
                    IsMulti(f) ? string.Join(", ", values.ToArray()) :
                                    values.FirstOrDefault();

                results.Add(new SubmittedFilter
                {
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    DataType = f.DataType,
                    Operator = f.Operator,
                    Value = displayValue
                });
            }

            if (!ModelState.IsValid)
            {
                // 驗證失敗 → 回填下拉清單、返回同頁顯示錯誤
                var selectDict = new Dictionary<int, SelectList>();
                foreach (var f in def.ReportFilters)
                    if (NeedsSelect(f)) selectDict[f.ReportFilterId] = BuildSelectListFromOptions(f.Options);
                ViewBag.Selects = selectDict;
                ViewBag.ReportName = def.ReportName;
                return View(def);
            }

            // 依按鈕分流：「產生圖表」或「匯出」
            var action = form["__action"].ToString();

            // 專用：從 results 抓出 StartDate/EndDate（可沒有）
            (DateTime? from, DateTime? to) = ParseDateRange(results);

            // ★ 這裡先用 ReportExportLog 當資料源：統計某報表在時間區間內被匯出的次數
            var query = _context.ReportExportLogs
                .Where(x => x.ReportName == def.ReportName);

            if (from.HasValue) query = query.Where(x => x.ExportAt >= from.Value);
            if (to.HasValue) query = query.Where(x => x.ExportAt <= to.Value);

            var points = await query
                .GroupBy(x => x.ExportAt!.Value.Date)
                .Select(g => new ChartPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderBy(p => p.Label)
                .ToListAsync();

            if (string.Equals(action, "export", StringComparison.OrdinalIgnoreCase))
            {
                // 匯出圖表資料（CSV）
                var bytes = BuildChartCsv(def.ReportName, points, from, to);

                // 記 Log（沿用你的 ExportLog）
                var filtersDict = results.ToDictionary(x => x.FieldName, x => x.Value ?? "");
                var filtersJson = System.Text.Json.JsonSerializer.Serialize(filtersDict);
                var (fileName, relativePath, physicalPath) = GetExportPaths(def.ReportName + "_chart", "csv", _env.WebRootPath);
                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
                System.IO.File.WriteAllBytes(physicalPath, bytes);

                _context.ReportExportLogs.Add(new ReportExportLog
                {
                    UserId = null,
                    ReportName = def.ReportName,
                    ExportFormat = "CSV",
                    ExportAt = DateTime.Now,
                    Filters = filtersJson,
                    FilePath = "/" + relativePath.Replace('\\', '/')
                });
                _context.ReportAccessLogs.Add(new ReportAccessLog
                {
                    UserId = null,
                    ReportDefinitionId = def.ReportDefinitionId,
                    AccessedAt = DateTime.Now,
                    ActionType = "chart-export",
                    Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                });
                await _context.SaveChangesAsync();

                return File(bytes, "text/csv", fileName);
            }

            // 顯示圖表（把資料交給 View）
            var vm = new ChartResultVM
            {
                ReportDefinitionId = def.ReportDefinitionId,
                ReportName = def.ReportName,
                From = from,
                To = to,
                Points = points
            };

            return View("ChartResult", vm); // 我們等下會在 Chart.cshtml 裡同時放 表單 + 圖表
        }

        // ===== 小工具 =====

        // 從 SubmittedFilter 嘗試抓出 StartDate/EndDate（欄位英文名稱）
        private static (DateTime? from, DateTime? to) ParseDateRange(IEnumerable<SubmittedFilter> filters)
        {
            DateTime? from = null, to = null;

            string? get(string key) =>
                filters.FirstOrDefault(x => x.FieldName.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

            // 允許三種來源：
            // 1) between: "2025-08-01 ~ 2025-08-31"
            // 2) StartDate / EndDate 各一欄
            // 3) 單一 date 欄位（就當 from）
            var between = filters.FirstOrDefault(x => x.Operator.Equals("between", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(between) && between.Contains("~"))
            {
                var parts = between.Split('~', 2, StringSplitOptions.TrimEntries);
                DateTime f, t;
                if (DateTime.TryParse(parts[0], out f)) from = f;
                if (DateTime.TryParse(parts[1], out t)) to = t;
            }
            else
            {
                if (DateTime.TryParse(get("StartDate"), out var f)) from = f;
                if (DateTime.TryParse(get("EndDate"), out var t)) to = t;
            }
            return (from, to);
        }

        // 匯出圖表資料成 CSV
        private static byte[] BuildChartCsv(string reportName, List<ChartPoint> points, DateTime? from, DateTime? to)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ReportName,{Escape(reportName)}");
            if (from.HasValue || to.HasValue)
                sb.AppendLine($"Range,{(from?.ToString("yyyy-MM-dd") ?? "")}~{(to?.ToString("yyyy-MM-dd") ?? "")}");
            sb.AppendLine();
            sb.AppendLine("Date,Count");

            foreach (var p in points)
                sb.AppendLine($"{p.Label:yyyy-MM-dd},{p.Value}");

            return System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

            static string Escape(string s) =>
                (s.Contains(',') || s.Contains('"')) ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }

        // 圖表資料點
        public class ChartPoint
        {
            public DateTime Label { get; set; }
            public int Value { get; set; }
        }

        // 圖表頁的 ViewModel
        public class ChartResultVM
        {
            public int ReportDefinitionId { get; set; }
            public string ReportName { get; set; } = null!;
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public List<ChartPoint> Points { get; set; } = new();
        }

    }
}
