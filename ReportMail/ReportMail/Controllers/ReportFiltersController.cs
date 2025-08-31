using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReportMail.Models;

namespace ReportMail.Controllers
{
    public class ReportFiltersController : Controller
    {
        private readonly ReportMailDbContext _context;

        public ReportFiltersController(ReportMailDbContext context)
        {
            _context = context;
        }

        // GET: ReportFilters
        public async Task<IActionResult> Index()
        {
            var reportMailDbContext = _context.ReportFilters.Include(r => r.ReportDefinition);
            return View(await reportMailDbContext.ToListAsync());
        }

        // GET: ReportFilters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportFilter = await _context.ReportFilters
                .Include(r => r.ReportDefinition)
                .FirstOrDefaultAsync(m => m.ReportFilterId == id);
            if (reportFilter == null)
            {
                return NotFound();
            }

            return View(reportFilter);
        }

        // GET: ReportFilters/Create
        public IActionResult Create()
        {
            ViewData["ReportDefinitionId"] = new SelectList(_context.ReportDefinitions, "ReportDefinitionId", "ReportDefinitionId");
            return View();
        }

        // POST: ReportFilters/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReportFilterId,ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive,CreatedAt,UpdatedAt")] ReportFilter reportFilter)
        {
            if (ModelState.IsValid)
            {
                _context.Add(reportFilter);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ReportDefinitionId"] = new SelectList(_context.ReportDefinitions, "ReportDefinitionId", "ReportDefinitionId", reportFilter.ReportDefinitionId);
            return View(reportFilter);
        }

        // GET: ReportFilters/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportFilter = await _context.ReportFilters.FindAsync(id);
            if (reportFilter == null)
            {
                return NotFound();
            }
            ViewData["ReportDefinitionId"] = new SelectList(_context.ReportDefinitions, "ReportDefinitionId", "ReportDefinitionId", reportFilter.ReportDefinitionId);
            return View(reportFilter);
        }

        // POST: ReportFilters/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReportFilterId,ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive,CreatedAt,UpdatedAt")] ReportFilter reportFilter)
        {
            if (id != reportFilter.ReportFilterId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reportFilter);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportFilterExists(reportFilter.ReportFilterId))
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
            ViewData["ReportDefinitionId"] = new SelectList(_context.ReportDefinitions, "ReportDefinitionId", "ReportDefinitionId", reportFilter.ReportDefinitionId);
            return View(reportFilter);
        }

        // GET: ReportFilters/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportFilter = await _context.ReportFilters
                .Include(r => r.ReportDefinition)
                .FirstOrDefaultAsync(m => m.ReportFilterId == id);
            if (reportFilter == null)
            {
                return NotFound();
            }

            return View(reportFilter);
        }

        // POST: ReportFilters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reportFilter = await _context.ReportFilters.FindAsync(id);
            if (reportFilter != null)
            {
                _context.ReportFilters.Remove(reportFilter);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReportFilterExists(int id)
        {
            return _context.ReportFilters.Any(e => e.ReportFilterId == id);
        }
    }
}
