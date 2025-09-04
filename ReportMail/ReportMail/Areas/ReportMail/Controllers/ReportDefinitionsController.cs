using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;
using ReportMail.Data.Entities;
using ReportMail.Services.Reports;
using System.Text.Json;


namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	public class ReportDefinitionsController : Controller
    {
        private readonly ReportMailDbContext _context;

        public ReportDefinitionsController(ReportMailDbContext context)
        {
            _context = context;
        }

        // GET: ReportDefinitions
        public async Task<IActionResult> Index()
        {
            return View(await _context.ReportDefinitions.ToListAsync());
        }

        // GET: ReportDefinitions/Details/5
        public async Task<IActionResult> Details(int? id)
        {

            if (BuiltinReports.IsBuiltin((int)id))
            {
                return NotFound(); // 或 Forbid(); 不要讓人操作到
            }
            if (id == null)
            {
                return NotFound();
            }

            var reportDefinition = await _context.ReportDefinitions
                .FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
            if (reportDefinition == null)
            {
                return NotFound();
            }

            return View(reportDefinition);
        }

        // GET: ReportDefinitions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ReportDefinitions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReportDefinitionID,ReportName,Category,Description,IsActive")] ReportDefinition reportDefinition, string? FiltersJson)
        {
            if (!ModelState.IsValid)
                return View(reportDefinition);

            _context.Add(reportDefinition);
            await _context.SaveChangesAsync(); // 先拿到新 ID

            if (!string.IsNullOrWhiteSpace(FiltersJson))
            {
                try
                {
                    var drafts = System.Text.Json.JsonSerializer.Deserialize<List<ReportFilterDraft>>(FiltersJson) ?? new();
                    int order = 1;
                    foreach (var d in drafts.OrderBy(x => x.OrderIndex ?? int.MaxValue))
                    {
                        var f = new ReportFilter
                        {
                            ReportDefinitionID = reportDefinition.ReportDefinitionID,
                            FieldName = d.FieldName ?? "",
                            DisplayName = d.DisplayName,
                            DataType = d.DataType,
                            Operator = d.Operator,
                            DefaultValue = d.DefaultValue,
                            Options = d.Options,
                            OrderIndex = d.OrderIndex ?? order++,
                            IsRequired = d.IsRequired ?? false,
                            IsActive = d.IsActive ?? true
                        };
                        _context.ReportFilters.Add(f);
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // 若 JSON 解析失敗，不影響報表本體建立
                    ModelState.AddModelError("", "篩選器無法解析：" + ex.Message);
                }
            }

            return RedirectToAction(nameof(Details), new { id = reportDefinition.ReportDefinitionID });
        }

        // 只給這支 action 用的簡單 DTO
        private class ReportFilterDraft
        {
            public string? FieldName { get; set; }
            public string? DisplayName { get; set; }
            public string? DataType { get; set; }
            public string? Operator { get; set; }
            public string? DefaultValue { get; set; }
            public string? Options { get; set; }
            public int? OrderIndex { get; set; }
            public bool? IsRequired { get; set; }
            public bool? IsActive { get; set; }
        }


        // GET: ReportDefinitions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (BuiltinReports.IsBuiltin((int)id))
            {
                return NotFound(); // 或 Forbid(); 不要讓人操作到
            }
            if (id == null)
            {
                return NotFound();
            }

            var reportDefinition = await _context.ReportDefinitions.FindAsync(id);
            if (reportDefinition == null)
            {
                return NotFound();
            }
            return View(reportDefinition);
        }

        // POST: ReportDefinitions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReportDefinitionID,ReportName,Category,Description,IsActive,CreatedAt,UpdatedAt")] ReportDefinition reportDefinition)
        {
            if (id != reportDefinition.ReportDefinitionID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reportDefinition);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportDefinitionExists(reportDefinition.ReportDefinitionID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(reportDefinition);
        }

        // GET: ReportDefinitions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (BuiltinReports.IsBuiltin((int)id))
            {
                return NotFound(); // 或 Forbid(); 不要讓人操作到
            }
            if (id == null)
            {
                return NotFound();
            }

            var reportDefinition = await _context.ReportDefinitions
                .FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
            if (reportDefinition == null)
            {
                return NotFound();
            }

            return View(reportDefinition);
        }

        // POST: ReportDefinitions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reportDefinition = await _context.ReportDefinitions.FindAsync(id);
            if (reportDefinition != null)
            {
                _context.ReportDefinitions.Remove(reportDefinition);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReportDefinitionExists(int id)
        {
            return _context.ReportDefinitions.Any(e => e.ReportDefinitionID == id);
        }
    }
}
