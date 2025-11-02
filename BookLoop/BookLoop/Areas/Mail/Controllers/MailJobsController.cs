// Areas/Mail/Controllers/MailJobsController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services.Mail; // 放 MailJobRunner 的命名空間
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoop.Areas.Mail.Controllers
{
    [Area("Mail")]
    public class MailJobsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBackgroundJobClient _bg;
        private readonly ILogger<MailJobsController> _logger;

        public MailJobsController(AppDbContext db, IBackgroundJobClient bg, ILogger<MailJobsController> logger)
        {
            _db = db;
            _bg = bg;
            _logger = logger;
        }

        // GET: /Mail/MailJobs
        // 簡易列表（最近 100 筆，可依需求再擴充查詢條件）
        [HttpGet]
        public async Task<IActionResult> Index(string? status = null, int? templateId = null)
        {
            var q = _db.MailJobs.AsNoTracking().OrderByDescending(x => x.JobId).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            if (templateId is not null) q = q.Where(x => x.TemplateId == templateId.Value);

            var data = await q.Take(100).ToListAsync();
            return View(data); // 你可先做個極簡 Index.cshtml 表格
        }

        // GET: /Mail/MailJobs/Create?templateId=123
        [HttpGet]
        public async Task<IActionResult> Create(int? templateId = null)
        {
            await PopulateMailJobSelectsAsync(templateId, null);
            return View(new MailJob { TemplateId = templateId ?? 0, SendAt = DateTime.Now.AddMinutes(10) });
        }

        // POST: /Mail/MailJobs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MailJob m)
        {
            // 基本驗證
            if (m.TemplateId <= 0)
                ModelState.AddModelError(nameof(m.TemplateId), "缺少 TemplateId。");

            if (m.TemplateVersionId <= 0)
                ModelState.AddModelError(nameof(m.TemplateVersionId), "請選擇要使用的模板版本。");

            if (string.IsNullOrWhiteSpace(m.SegmentQuery))
                ModelState.AddModelError(nameof(m.SegmentQuery), "請貼上收件者名單（每行一筆：email[,name]）。");

            // 檢查版本是否存在且隸屬該 Template
            var versionExists = await _db.TemplateVersions
                .AnyAsync(v => v.TemplateId == m.TemplateId && v.TemplateVersionId == m.TemplateVersionId);

            if (!versionExists)
                ModelState.AddModelError(nameof(m.TemplateVersionId), "找不到對應的模板版本。");

            if (!ModelState.IsValid)
            {
                await PopulateMailJobSelectsAsync(m.TemplateId, m.TemplateVersionId); // 驗證失敗也要重塞下拉
                return View(m);
            }

            // 1) 先存批次
            m.Status = "Scheduled";
            m.CreatedAt = DateTime.Now;                // 本地時間
            m.CreatedBy = User?.Identity?.Name ?? "system";
            m.TemplateKey = m.TemplateKey?.Trim() ?? "";
            m.CampaignName = m.CampaignName?.Trim();
            _db.MailJobs.Add(m);
            await _db.SaveChangesAsync();              // 拿到 m.JobId

            // 2) 展開名單 → MailJobRecipient
            var recipients = m.SegmentQuery?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line =>
                {
                    var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
                    var email = parts[0];
                    var name = parts.Length > 1 ? parts[1] : null;
                    return string.IsNullOrWhiteSpace(email) ? null : new MailJobRecipient
                    {
                        MailJobId = m.JobId,
                        RecipientEmail = email,
                        RecipientName = name,
                        Status = "Pending"
                    };
                })
                .Where(x => x != null)! // 過濾空行
                .ToList() ?? new List<MailJobRecipient>();

            if (recipients.Count == 0)
            {
                ModelState.AddModelError(nameof(m.SegmentQuery), "名單為空或格式不正確");
                // 重新載入版本下拉… return View(m);
            }

            await _db.MailJobRecipients.AddRangeAsync(recipients);
            m.TotalRecipients = recipients.Count;
            await _db.SaveChangesAsync();

            // 3) 依 SendAt 排程或立即執行（用本地時間）
            if (m.SendAt <= DateTime.Now.AddSeconds(30))
            {
                BackgroundJob.Enqueue<IMailJobRunner>(r => r.RunAsync(m.JobId, CancellationToken.None));
                TempData["ok"] = $"已建立 Job #{m.JobId} 並立即開始執行。";
            }
            else
            {
                var delay = m.SendAt - DateTime.Now;
                BackgroundJob.Schedule<IMailJobRunner>(r => r.RunAsync(m.JobId, CancellationToken.None), delay);
                TempData["ok"] = $"已建立排程 Job #{m.JobId}，將於 {m.SendAt:yyyy/MM/dd HH:mm} 執行。";
            }

            return RedirectToAction(nameof(Details), new { id = m.JobId });
        }

        // GET: /Mail/MailJobs/Details/123
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var job = await _db.MailJobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == id);
            if (job == null) return NotFound();

            var recipients = await _db.MailJobRecipients.AsNoTracking()
                .Where(x => x.MailJobId == id)
                .OrderBy(x => x.MailJobRecipientId)
                .Take(500)
                .ToListAsync();

            var logs = await _db.MailSendLogs.AsNoTracking()
                .Where(x => x.MailJobId == id && x.JobRecipientId != null)
                .OrderByDescending(x => x.LogId)
                .Take(200)
                .ToListAsync();

            ViewBag.Recipients = recipients;
            ViewBag.Logs = logs;
            ViewBag.Progress = $"{job.SentCount} / {job.TotalRecipients}";
            return View(job);
        }

        // POST: /Mail/MailJobs/Cancel/123
        // MVP：僅改狀態為 Cancelled；若要“強制中止”正在跑的 Job，需在 Runner 加 CancellationToken 來源管理
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            var m = await _db.MailJobs.FirstOrDefaultAsync(x => x.JobId == id);
            if (m == null) return NotFound();

            if (m.Status is "Sending" or "Scheduled")
            {
                m.Status = "Canceled";
                await _db.SaveChangesAsync();
                TempData["ok"] = $"Job #{id} 已取消。";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==== Helpers ====
        // 共用：Create/Edit 頁所需下拉
        private async Task PopulateMailJobSelectsAsync(int? templateId = null, int? templateVersionId = null)
        {
            // Templates 下拉
            var templateItems = await _db.Templates
                .AsNoTracking()
                .OrderBy(t => t.TemplateKey)
                .Select(t => new SelectListItem
                {
                    Value = t.TemplateId.ToString(),
                    Text = t.TemplateKey,
                    Selected = (templateId != null && t.TemplateId == templateId.Value)
                })
                .ToListAsync();

            ViewBag.TemplateId = templateItems;

            // Versions 下拉（若未選 Template → 給空清單）
            List<SelectListItem> versionItems;
            if (templateId.HasValue)
            {
                versionItems = await _db.TemplateVersions
                    .AsNoTracking()
                    .Where(v => v.TemplateId == templateId.Value && v.IsActive)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenByDescending(v => v.UpdatedAt)
                    .Select(v => new SelectListItem
                    {
                        Value = v.TemplateVersionId.ToString(),
                        Text = (v.IsDefault ? "★ " : "") + (v.TemplateName ?? ("v" + v.TemplateVersionId)),
                        Selected = (templateVersionId != null && v.TemplateVersionId == templateVersionId.Value)
                    })
                    .ToListAsync();
            }
            else
            {
                versionItems = new List<SelectListItem>();
            }

            ViewBag.TemplateVersionId = versionItems;
        }

        // AJAX：依 Template 取版本清單
        [HttpGet]
        public async Task<IActionResult> GetVersions(int templateId)
        {
            var versions = await _db.TemplateVersions
                .AsNoTracking()
                .Where(v => v.TemplateId == templateId && v.IsActive)
                .OrderByDescending(v => v.IsDefault)
                .ThenByDescending(v => v.UpdatedAt)
                .Select(v => new
                {
                    id = v.TemplateVersionId,
                    text = (v.IsDefault ? "★ " : "") + (v.TemplateName ?? ("v" + v.TemplateVersionId))
                })
                .ToListAsync();

            return Json(versions);
        }
        /// <summary>
        /// 把未指定 Kind 的時間視為台北時間並轉換成 UTC；若已是 UTC 就直接回傳。
        /// </summary>
        private static DateTime NormalizeToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;

            // 你的系統時區：Asia/Taipei
            try
            {
                // Windows: "Taipei Standard Time"；Linux: "Asia/Taipei"
                var tz = TimeZoneInfo.FindSystemTimeZoneById(
#if WINDOWS
                    "Taipei Standard Time"
#else
                    "Asia/Taipei"
#endif
                );
                if (dt.Kind == DateTimeKind.Unspecified)
                    return TimeZoneInfo.ConvertTimeToUtc(dt, tz);
                if (dt.Kind == DateTimeKind.Local)
                    return dt.ToUniversalTime();
            }
            catch
            {
                // 找不到時區設定就退而求其次：當成本地時間轉 UTC
                return dt.ToUniversalTime();
            }
            return dt.ToUniversalTime();
        }
    }
}
