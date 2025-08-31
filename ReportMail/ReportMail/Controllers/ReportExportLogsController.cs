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
    public class ReportExportLogsController : Controller
    {
        private readonly ReportMailDbContext _context;

        public ReportExportLogsController(ReportMailDbContext context)
        {
            _context = context;
        }

        // GET: ReportExportLogs
        public async Task<IActionResult> Index()
        {
            return View(await _context.ReportExportLogs.ToListAsync());
        }

        // GET: ReportExportLogs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportExportLog = await _context.ReportExportLogs
                .FirstOrDefaultAsync(m => m.ExportId == id);
            if (reportExportLog == null)
            {
                return NotFound();
            }

            return View(reportExportLog);
        }

        // GET: ReportExportLogs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ReportExportLogs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExportId,UserId,ReportName,ExportFormat,ExportAt,Filters,FilePath")] ReportExportLog reportExportLog)
        {
            if (ModelState.IsValid)
            {
                _context.Add(reportExportLog);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(reportExportLog);
        }

        // GET: ReportExportLogs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportExportLog = await _context.ReportExportLogs.FindAsync(id);
            if (reportExportLog == null)
            {
                return NotFound();
            }
            return View(reportExportLog);
        }

        // POST: ReportExportLogs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExportId,UserId,ReportName,ExportFormat,ExportAt,Filters,FilePath")] ReportExportLog reportExportLog)
        {
            if (id != reportExportLog.ExportId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reportExportLog);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportExportLogExists(reportExportLog.ExportId))
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
            return View(reportExportLog);
        }

        // GET: ReportExportLogs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportExportLog = await _context.ReportExportLogs
                .FirstOrDefaultAsync(m => m.ExportId == id);
            if (reportExportLog == null)
            {
                return NotFound();
            }

            return View(reportExportLog);
        }

        // POST: ReportExportLogs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reportExportLog = await _context.ReportExportLogs.FindAsync(id);
            if (reportExportLog != null)
            {
                _context.ReportExportLogs.Remove(reportExportLog);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReportExportLogExists(int id)
        {
            return _context.ReportExportLogs.Any(e => e.ExportId == id);
        }
    }
}
