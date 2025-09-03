// Areas/ReportMail/Controllers/FiltersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;
using ReportMail.Data.Entities;
using ReportMail.Models.ReportFilters;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    //[Authorize(Roles = "Admin")] // 視需求
    public class FiltersController : Controller
    {
        private readonly ReportMailDbContext _db;
        public FiltersController(ReportMailDbContext db) => _db = db;

        // List：顯示某報表的所有（非底線）篩選
        public async Task<IActionResult> Index(int reportDefinitionId)
        {
            var report = await _db.ReportDefinitions.FindAsync(reportDefinitionId);
            if (report is null) return NotFound();

            var filters = await _db.ReportFilters.AsNoTracking()
                .Where(f => f.ReportDefinitionID == reportDefinitionId && !f.FieldName.StartsWith("_"))
                .OrderBy(f => f.OrderIndex).ToListAsync();

            return View(new FiltersIndexVM { Report = report, Items = filters });
        }

        // Create（用我們之前給你的 FilterDesignerVM 來一次展開 1~多列）
        [HttpGet]
        public IActionResult Create(int reportDefinitionId) =>
            View(new FilterDesignerVM { ReportDefinitionId = reportDefinitionId });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FilterDesignerVM vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var now = DateTime.Now;
            foreach (var r in vm.ExpandToRows())
            {
                _db.ReportFilters.Add(new ReportFilter
                {
                    ReportDefinitionID = vm.ReportDefinitionId,
                    FieldName = r.FieldName,
                    DisplayName = r.DisplayName,
                    DataType = r.DataType,
                    Operator = r.Operator,
                    DefaultValue = r.DefaultValueJson,
                    Options = r.OptionsJson,
                    OrderIndex = r.OrderIndex,
                    IsRequired = r.IsRequired,
                    IsActive = r.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { reportDefinitionId = vm.ReportDefinitionId });
        }

        // Edit：單筆 ReportFilter 編輯（不開放底線 _meta）
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var f = await _db.ReportFilters.FindAsync(id);
            if (f is null || f.FieldName.StartsWith("_")) return NotFound();

            var vm = FilterEditVM.FromEntity(f); // 下面有 VM
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FilterEditVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var f = await _db.ReportFilters.FindAsync(vm.ReportFilterID);
            if (f is null || f.FieldName.StartsWith("_")) return NotFound();

            // 伺服器端保護：型別/運算子白名單，避免違反 DB CHECK
            if (!Allowed.Types.Contains(vm.DataType) || !Allowed.Ops.Contains(vm.Operator))
            { ModelState.AddModelError("", "資料型態或比較子不合法"); return View(vm); }

            f.DisplayName = vm.DisplayName;
            f.DataType = vm.DataType;
            f.Operator = vm.Operator;
            f.DefaultValue = vm.DefaultValueJson;
            f.Options = vm.BuildOptionsJson(); // 內含 column/items
            f.OrderIndex = vm.OrderIndex;
            f.IsRequired = vm.IsRequired;
            f.IsActive = vm.IsActive;
            f.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { reportDefinitionId = f.ReportDefinitionID });
        }

        // Delete：硬刪或軟刪（二擇一）
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var f = await _db.ReportFilters.FindAsync(id);
            if (f is null || f.FieldName.StartsWith("_")) return NotFound();

            _db.ReportFilters.Remove(f); // 若想軟刪：改成 f.IsActive=false; f.UpdatedAt=Now;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { reportDefinitionId = f.ReportDefinitionID });
        }

        // Reorder：前端拖曳後送上 [ (id,orderIndex), ... ]
        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] List<(int id, int order)> items)
        {
            var ids = items.Select(x => x.id).ToHashSet();
            var rows = await _db.ReportFilters.Where(x => ids.Contains(x.ReportFilterID)).ToListAsync();
            foreach (var r in rows)
                r.OrderIndex = items.First(x => x.id == r.ReportFilterID).order;

            await _db.SaveChangesAsync();
            return Ok();
        }

        private static class Allowed
        {
            public static readonly HashSet<string> Types =
                new(StringComparer.OrdinalIgnoreCase) { "date", "int", "decimal", "string", "boolean", "select", "multiselect" };
            public static readonly HashSet<string> Ops =
                new(StringComparer.OrdinalIgnoreCase) { "eq", "ne", "gt", "gte", "lt", "lte", "like", "in", "between" /*如需*/ };
        }
    }

}
