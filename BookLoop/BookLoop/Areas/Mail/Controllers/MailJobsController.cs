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
        public async Task<IActionResult> Create(int templateId)
        {
            // 載入版本清單（僅顯示啟用版本，預設版排前面）
            var versions = await _db.TemplateVersions
                .AsNoTracking()
                .Where(v => v.TemplateId == templateId && v.IsActive)
                .OrderByDescending(v => v.IsDefault)
                .ThenByDescending(v => v.UpdatedAt)
                .Select(v => new { v.TemplateVersionId, Text = (v.IsDefault ? "★ " : "") + (v.TemplateName ?? ("v" + v.TemplateVersionId)) })
                .ToListAsync();

            ViewBag.TemplateVersions = new SelectList(versions, "TemplateVersionId", "Text");

            var m = new MailJob
            {
                TemplateId = templateId,
                // 預設下一個十分鐘（以台北時區顯示的話，View 自行註記；此處存的屬性本身為 DateTime）
                SendAt = DateTime.Now.AddMinutes(10)
            };
            return View(m); // 你可先做個極簡 Create.cshtml 表單
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
                // 重新載入版本下拉選單
                var versions = await _db.TemplateVersions
                    .AsNoTracking()
                    .Where(v => v.TemplateId == m.TemplateId && v.IsActive)
                    .OrderByDescending(v => v.IsDefault)
                    .ThenByDescending(v => v.UpdatedAt)
                    .Select(v => new { v.TemplateVersionId, Text = (v.IsDefault ? "★ " : "") + (v.TemplateName ?? ("v" + v.TemplateVersionId)) })
                    .ToListAsync();
                ViewBag.TemplateVersions = new SelectList(versions, "TemplateVersionId", "Text", m.TemplateVersionId);
                return View(m);
            }

            // 正規化 SendAt：若是未指定 Kind 的本地時間，視為台北時間轉 UTC 儲存；若是 UTC 則原樣。
            // （若你的欄位就是用本地時間保存，可移除此轉換）
            var sendAt = NormalizeToUtc(m.SendAt);

            m.SendAt = sendAt;                 // 以 UTC 保存
            m.Status = "Scheduled";
            m.CreatedAt = DateTime.UtcNow;
            m.CreatedBy = User?.Identity?.Name ?? "system";
            // 建議把 TemplateKey/CampaignName 也一起帶上（若 View 有提供）
            m.TemplateKey = m.TemplateKey?.Trim() ?? "";
            m.CampaignName = m.CampaignName?.Trim();

            _db.MailJobs.Add(m);
            await _db.SaveChangesAsync();

            // 若時間在 30 秒內 → 立即執行；否則排到指定時間
            if (sendAt <= DateTime.UtcNow.AddSeconds(30))
            {
                _bg.Enqueue<MailJobRunner>(r => r.RunAsync(m.JobId, CancellationToken.None));
                TempData["ok"] = $"已建立 Job #{m.JobId} 並立即開始執行。";
            }
            else
            {
                BackgroundJob.Schedule<MailJobRunner>(r => r.RunAsync(m.JobId, CancellationToken.None), sendAt);
                TempData["ok"] = $"已建立排程 Job #{m.JobId}，將於 {sendAt:u} (UTC) 執行。";
            }

            return RedirectToAction(nameof(Details), new { id = m.JobId });
        }

        // GET: /Mail/MailJobs/Details/123
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var m = await _db.MailJobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == id);
            if (m == null) return NotFound();
            return View(m); // 你可在 View 顯示 Job 基本資訊、狀態、錯誤、Segment 前 10 筆等
        }

        // POST: /Mail/MailJobs/Cancel/123
        // MVP：僅改狀態為 Cancelled；若要“強制中止”正在跑的 Job，需在 Runner 加 CancellationToken 來源管理
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id)
        {
            var m = await _db.MailJobs.FirstOrDefaultAsync(x => x.JobId == id);
            if (m == null) return NotFound();

            if (m.Status is "Running" or "Scheduled")
            {
                m.Status = "Cancelled";
                await _db.SaveChangesAsync();
                TempData["ok"] = $"Job #{id} 已設為 Cancelled。";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==== Helpers ====

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
