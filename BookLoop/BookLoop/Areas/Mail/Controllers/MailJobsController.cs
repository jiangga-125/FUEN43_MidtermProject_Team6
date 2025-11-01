using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Areas.Mail.Controllers
{
    [Area("Mail")]
    public class MailJobsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IMailService _mail;

        public MailJobsController(AppDbContext db, IMailService mail)
        {
            _db = db;
            _mail = mail;
        }

        // =======================
        // Index: 群發列表
        // =======================
        public async Task<IActionResult> Index()
        {
            var list = await _db.MailJobs
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return View(list);
        }

        // =======================
        // Create: 建立群發活動
        // =======================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Templates = await _db.Templates
                .Select(t => new { t.TemplateId, t.TemplateKey })
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MailJob job)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Templates = await _db.Templates.ToListAsync();
                return View(job);
            }

            job.Status = "Scheduled";
            job.CreatedAt = DateTime.Now;

            _db.MailJobs.Add(job);
            await _db.SaveChangesAsync();
            TempData["msg"] = "活動建立成功，可進入明細頁手動寄送。";
            return RedirectToAction(nameof(Details), new { id = job.JobId });
        }

        // =======================
        // Details: 顯示活動內容與狀態
        // =======================
        public async Task<IActionResult> Details(long id)
        {
            var job = await _db.MailJobs.FindAsync(id);
            if (job == null) return NotFound();

            var sent = await _db.MailSendLogs
                .Where(x => x.MailJobId == id)
                .OrderByDescending(x => x.SentAt)
                .ToListAsync();

            ViewBag.Logs = sent;
            return View(job);
        }

        // =======================
        // Run: 手動執行寄送
        // =======================
        [HttpPost]
        public async Task<IActionResult> Run(long id)
        {
            var job = await _db.MailJobs.FindAsync(id);
            if (job == null) return NotFound();

            var templateVersion = await _db.TemplateVersions.FindAsync(job.TemplateVersionId);
            if (templateVersion == null)
            {
                TempData["err"] = "找不到對應的模板版本。";
                return RedirectToAction(nameof(Details), new { id });
            }

            // 解析 SegmentQuery：一行或逗號分隔 Email
            var emails = job.SegmentQuery
                .Replace(",", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Contains("@"))
                .ToList();

            if (emails.Count == 0)
            {
                TempData["err"] = "未提供任何有效收件人。";
                return RedirectToAction(nameof(Details), new { id });
            }

            int sentCount = 0, failCount = 0;
            foreach (var to in emails)
            {
                var log = new MailSendLog
                {
                    Category = "Bulk",
                    TemplateId = job.TemplateId,
                    TemplateKey = job.TemplateKey,
                    TemplateVersionId = job.TemplateVersionId,
                    MailJobId = job.JobId,
                    Recipient = to,
                    Subject = templateVersion.Subject,
                    Status = "Pending",
                    SentAt = DateTime.Now
                };
                _db.MailSendLogs.Add(log);
                await _db.SaveChangesAsync();

                try
                {
                    await _mail.SendAsync(to, templateVersion.Subject, templateVersion.BodyHtml);
                    log.Status = "Sent";
                    sentCount++;
                }
                catch (Exception ex)
                {
                    log.Status = "Failed";
                    log.Error = ex.Message;
                    failCount++;
                }
                await _db.SaveChangesAsync();
            }

            job.Status = "Done";
            await _db.SaveChangesAsync();

            TempData["msg"] = $"寄送完成，共 {sentCount} 封成功，{failCount} 封失敗。";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
