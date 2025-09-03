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
        public async Task<IActionResult> Create([Bind("ReportDefinitionID,ReportName,Category,Description,IsActive,CreatedAt,UpdatedAt")] ReportDefinition reportDefinition)
        {
            if (ModelState.IsValid)
            {
                _context.Add(reportDefinition);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(reportDefinition);
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
