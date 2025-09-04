using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;
using ReportMail.Data.Entities;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Route("ReportMail/[controller]/[action]")]
    public class FilterTemplatesController : Controller
    {
        private readonly ReportMailDbContext _db;
        public FilterTemplatesController(ReportMailDbContext db) => _db = db;

        /// <summary>
        /// 一鍵建立模板：line.sales / line.borrow / bar.sales / bar.borrow / pie.sales / pie.borrow
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromTemplate(int reportDefinitionId, string preset)
        {
            var def = await _db.ReportDefinitions.AsNoTracking()
                          .FirstOrDefaultAsync(x => x.ReportDefinitionID == reportDefinitionId);
            if (def == null) return NotFound("ReportDefinition not found.");

            var startOrder = await _db.ReportFilters
                .Where(f => f.ReportDefinitionID == reportDefinitionId)
                .Select(f => (int?)f.OrderIndex).DefaultIfEmpty(0).MaxAsync() ?? 0;

            var toAdd = BuildTemplateFilters(def.Category, preset, reportDefinitionId, startOrder + 1);

            // 避免重複（同一 ReportDefinitionID + FieldName 已存在就跳過）
            var existed = await _db.ReportFilters
                .Where(f => f.ReportDefinitionID == reportDefinitionId)
                .Select(f => f.FieldName).ToListAsync();

            var finalAdd = toAdd.Where(f => !existed.Contains(f.FieldName)).ToList();
            if (finalAdd.Count == 0)
            {
                TempData["Msg"] = "沒有需要新增的欄位（可能都已存在）。";
                return RedirectToAction("Index", "Filters", new { area = "ReportMail" });
            }

            _db.ReportFilters.AddRange(finalAdd);
            await _db.SaveChangesAsync();

            TempData["Msg"] = $"已建立 {finalAdd.Count} 個篩選欄位。";
            return RedirectToAction("Details", "ReportDefinitions", new { area = "ReportMail", id = reportDefinitionId });
        }

        private static List<ReportFilter> BuildTemplateFilters(string category, string preset, int reportDefinitionId, int startOrder)
        {
            category = (category ?? "").ToLowerInvariant();
            preset = (preset ?? "").ToLowerInvariant();

            var list = new List<ReportFilter>();
            int ord = startOrder;

            string Json(object o) => JsonSerializer.Serialize(o);

            // 共用選項 JSON
            var granularityOpts = Json(new { granularity = new[] { "day", "month", "year" } });
            var priceRanges = Json(new { ranges = Enumerable.Range(0, 10).Select(i => $"{i * 100 + 1}-{(i + 1) * 100}").ToArray() });

            // 出版年份（每 10 年一段，1901~今年）
            var nowYear = DateTime.Now.Year;
            var decades = new List<string>();
            for (int y = 1901; y <= nowYear; y += 10)
            {
                var end = Math.Min(y + 9, nowYear);
                decades.Add($"{y}-{end}");
            }
            var decadeRanges = Json(new { ranges = decades.ToArray() });

            // 排名區間（1~10 預設）
            const string rankDefault = "1-10";

            switch (preset)
            {
                // 折線圖：書籍銷售量（本）
                case "line.sales":
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "OrderDate", DisplayName = "日期區間", DataType = "date", Operator = "between", Options = granularityOpts, OrderIndex = ord++, IsRequired = true, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "CategoryID", DisplayName = "書籍種類", DataType = "select", Operator = "in", Options = Json(new { source = "Categories" }), OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "SalePrice", DisplayName = "單本價位", DataType = "select", Operator = "between", Options = priceRanges, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    break;

                // 折線圖：書籍借閱量（本）
                case "line.borrow":
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "BorrowDate", DisplayName = "日期區間", DataType = "date", Operator = "between", Options = granularityOpts, OrderIndex = ord++, IsRequired = true, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "CategoryID", DisplayName = "書籍種類", DataType = "select", Operator = "in", Options = Json(new { source = "Categories" }), OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "PublishYear", DisplayName = "出版年份(每10年)", DataType = "select", Operator = "between", Options = decadeRanges, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    break;

                // 長條／圓餅：銷售量排行/組成
                case "bar.sales":
                case "pie.sales":
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "DateRange", DisplayName = "日期範圍", DataType = "date", Operator = "between", Options = null, OrderIndex = ord++, IsRequired = true, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "Rank", DisplayName = "排行名次區間", DataType = "number", Operator = "between", DefaultValue = rankDefault, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "CategoryID", DisplayName = "書籍種類", DataType = "select", Operator = "in", Options = Json(new { source = "Categories" }), OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "SalePrice", DisplayName = "單本價位", DataType = "select", Operator = "between", Options = priceRanges, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    break;

                // 長條／圓餅：借閱量排行/組成
                case "bar.borrow":
                case "pie.borrow":
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "DateRange", DisplayName = "日期範圍", DataType = "date", Operator = "between", Options = null, OrderIndex = ord++, IsRequired = true, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "Rank", DisplayName = "排行名次區間", DataType = "number", Operator = "between", DefaultValue = rankDefault, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "CategoryID", DisplayName = "書籍種類", DataType = "select", Operator = "in", Options = Json(new { source = "Categories" }), OrderIndex = ord++, IsRequired = false, IsActive = true });
                    list.Add(new ReportFilter { ReportDefinitionID = reportDefinitionId, FieldName = "PublishYear", DisplayName = "出版年份(每10年)", DataType = "select", Operator = "between", Options = decadeRanges, OrderIndex = ord++, IsRequired = false, IsActive = true });
                    break;

                default:
                    // 未指定時依報表類別選最常用模板
                    if (category == "line") return BuildTemplateFilters(category, "line.sales", reportDefinitionId, startOrder);
                    if (category == "bar") return BuildTemplateFilters(category, "bar.sales", reportDefinitionId, startOrder);
                    if (category == "pie") return BuildTemplateFilters(category, "pie.sales", reportDefinitionId, startOrder);
                    break;
            }
            return list;
        }
    }
}
