// Controllers/ReportsController.cs
using ClosedXML.Excel;                       // 匯出
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;              // DbContext
using ReportMail.Data.Entities;
using ReportMail.Services.Reports;
using System.Globalization;
using System.Text.Json;

namespace ReportMail.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ReportMailDbContext _db;
        private readonly ReportQueryBuilder _builder;
        public ReportsController(ReportMailDbContext db, ReportQueryBuilder builder)
        {
            _db = db;
            _builder = builder;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? reportDefinitionId)
        {
            var defs = await _db.Set<ReportDefinition>()      // 不靠 DbSet 名稱，直接用型別
                .AsNoTracking()
                .OrderBy(x => x.ReportName)                   // 有這欄就留著，沒有就拿掉
                .Select(x => new { Value = x.ReportDefinitionID, Text = x.ReportName })
                .ToListAsync();

            ViewBag.ReportDefs = defs;

            var rid = reportDefinitionId ?? defs.FirstOrDefault()?.Value ?? 0;

            // 只給畫面的 filters（排除 meta）
            var filters = await _db.Set<ReportFilter>().AsNoTracking()
              .Where(f => f.ReportDefinitionID == rid && f.IsActive && !f.FieldName.StartsWith("_"))
              .OrderBy(f => f.OrderIndex)
              .ToListAsync();
            ViewBag.Filters = filters;
            ViewBag.SelectedReportId = rid;

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> Run(int reportDefinitionId)
        {
            try
            {
                var recipe = await LoadRecipeAsync(reportDefinitionId);
                //先驗證 meta 是否齊全欄位有值
                if (string.IsNullOrWhiteSpace(recipe.Dimension?.Column))
                    return BadRequest("_dimension.options.column 未設定");
                if (string.IsNullOrWhiteSpace(recipe.Metric?.Expr))
                    return BadRequest("_metric.options.expr 未設定");

                if (recipe == null) return BadRequest("報表缺少 meta 設定");

                var uiFilters = await _db.Set<ReportFilter>().AsNoTracking()
                    .Where(f => f.ReportDefinitionID == reportDefinitionId && f.IsActive && !f.FieldName.StartsWith("_"))
                    .OrderBy(f => f.OrderIndex)
                    .Select(f => new { f.FieldName, f.DataType, f.Operator, f.Options })
                    .ToListAsync();

                var (sql, ps) = _builder.Build(
                    recipe,
                    uiFilters.Select(f => (f.FieldName, f.DataType ?? "string", f.Operator ?? "eq", f.Options)),
                    Request.Query
                );
                //暫時如果還有 500，彈窗會是清楚的 SQL/轉型訊息
                Console.WriteLine(sql);
                foreach (var p in ps) Console.WriteLine($"{p.ParameterName} = {p.Value}");

                var rows = await _db.ChartPoints.FromSqlRaw(sql, ps.ToArray()).ToListAsync();
                var labels = rows.Select(r => r.Label).ToArray();
                var data = rows.Select(r => r.Value).ToArray();
                return Json(new { labels, data });


            }
            catch (Exception ex)
            {
                // 給前端看得懂的純文字
                return StatusCode(500, ex.GetBaseException().Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int reportDefinitionId)
        {
            try
            {
                var recipe = await LoadRecipeAsync(reportDefinitionId);
                if (recipe == null) return BadRequest("報表缺少 meta 設定");

                var uiFilters = await _db.Set<ReportFilter>().AsNoTracking()
                    .Where(f => f.ReportDefinitionID == reportDefinitionId && f.IsActive && !f.FieldName.StartsWith("_"))
                    .OrderBy(f => f.OrderIndex)
                    .Select(f => new { f.FieldName, f.DataType, f.Operator, f.Options })
                    .ToListAsync();

                var (sql, ps) = _builder.Build(
                    recipe,
                    uiFilters.Select(f => (f.FieldName, f.DataType ?? "string", f.Operator ?? "eq", f.Options)),
                    Request.Query
                );

                var rows = await _db.ChartPoints.FromSqlRaw(sql, ps.ToArray()).ToListAsync();

                using var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Report");
                ws.Cell(1, 1).Value = "Label";
                ws.Cell(1, 2).Value = "Value";
                for (int i = 0; i < rows.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = rows[i].Label;
                    ws.Cell(i + 2, 2).Value = rows[i].Value;
                }
                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms); ms.Position = 0;
                return File(ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Report_{reportDefinitionId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.GetBaseException().Message);
            }
        }


        private static readonly JsonSerializerOptions JsonOpt = new()
        {
            PropertyNameCaseInsensitive = true // ← 關鍵：忽略大小寫
        };

        // 讀 meta rows → 組出 Recipe
        private async Task<ReportRecipe?> LoadRecipeAsync(int reportDefinitionId)
        {
            // 只抓 meta（FieldName 以底線開頭）
            var meta = await _db.Set<ReportFilter>().AsNoTracking()
                .Where(f => f.ReportDefinitionID == reportDefinitionId && f.FieldName.StartsWith("_"))
                .ToDictionaryAsync(f => f.FieldName, f => f.Options ?? "{}");

            if (!meta.TryGetValue("_source", out var srcJson) ||
                !meta.TryGetValue("_dimension", out var dimJson) ||
                !meta.TryGetValue("_metric", out var metJson))
                return null; // 缺 meta 直接回 null，外層回 400

            // 大小寫不敏感的反序列化（這裡才會把 "column" 填進 Column）
            var src = JsonSerializer.Deserialize<ReportSource>(srcJson, JsonOpt)!;
            var dim = JsonSerializer.Deserialize<ReportDimension>(dimJson, JsonOpt)!;
            var met = JsonSerializer.Deserialize<ReportMetric>(metJson, JsonOpt)!;

            // _preset.where（可有可無）
            string? presetWhere = null;
            if (meta.TryGetValue("_preset", out var preJson))
            {
                using var doc = JsonDocument.Parse(preJson);
                if (doc.RootElement.TryGetProperty("where", out var w))
                    presetWhere = w.GetString();
            }

            return new ReportRecipe
            {
                Source = src,
                Dimension = dim,
                Metric = met,
                WherePreset = presetWhere
            };
        }

        private static DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd", "MM/dd", "MM/dd/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(s, out dt)) return dt;
            return null;
        }
    }
}
