using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReportMail.Models;

namespace ReportMail.Controllers
{
    public class ReportExportLogsController : Controller
    {
        private readonly ReportMailDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportExportLogsController(ReportMailDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: ReportExportLogs
        public async Task<IActionResult> Index()
        {
            var items = await _context.ReportExportLogs.OrderByDescending(x => x.ExportAt).ToListAsync();
            return View(items);
        }

        // GET: /ReportExportLogs/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var log = await _context.ReportExportLogs.FirstOrDefaultAsync(x => x.ExportId == id);
            if (log == null || string.IsNullOrWhiteSpace(log.FilePath))
                return NotFound("找不到匯出紀錄或檔案路徑。");

            // 1) 解析實體路徑：
            //    - 以 "/" 或 "~/" 開頭 → 當成 **相對於 wwwroot** 的路徑
            //    - 絕對路徑（C:\... 或 /var/...） → 直接使用
            //    - 其他 → 也當作相對於 wwwroot
            string physical;
            if (Path.IsPathRooted(log.FilePath))
            {
                physical = log.FilePath;
            }
            else
            {
                var rel = log.FilePath.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                physical = Path.Combine(_env.WebRootPath, rel); // wwwroot/exports/xxx
            }

            if (!System.IO.File.Exists(physical))
                return NotFound($"檔案不存在：{physical}");

            // 2) 推斷 Content-Type（pdf / csv / xlsx …）
            var fileName = Path.GetFileName(physical);
            var contentType = GetContentTypeByExtension(Path.GetExtension(physical));

            // 3) 回傳實體檔案
            return PhysicalFile(physical, contentType, fileName);
        }

        // 小工具：副檔名對應 MIME
        private static string GetContentTypeByExtension(string? ext)
        {
            ext = (ext ?? string.Empty).ToLowerInvariant();
            return ext switch
            {
                ".csv" => "text/csv",
                ".pdf" => "application/pdf",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
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
