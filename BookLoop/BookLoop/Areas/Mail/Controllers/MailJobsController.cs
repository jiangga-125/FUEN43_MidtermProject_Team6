// Areas/Mail/Controllers/MailJobsController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Areas.Mail.ViewModels;
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

            //提供會員性別的下拉選單
            ViewBag.MemberGender = new List<SelectListItem>
    {
        new SelectListItem { Value = "0", Text = "0 - 未知" },
        new SelectListItem { Value = "1", Text = "1 - 男性" },
		new SelectListItem { Value = "2", Text = "2 - 女性" },
	};
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

            // 檢查版本並取得 Template 物件
            var template = await _db.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateId == m.TemplateId);

            if (template == null)
            {
                ModelState.AddModelError(nameof(m.TemplateId), "找不到對應的模板群組。");
            }
            else
            {
                // 順便檢查版本是否存在
                var versionExists = await _db.TemplateVersions
                    .AnyAsync(v => v.TemplateId == m.TemplateId && v.TemplateVersionId == m.TemplateVersionId);

                if (!versionExists)
                    ModelState.AddModelError(nameof(m.TemplateVersionId), "找不到對應的模板版本。");
            }
            if (!ModelState.IsValid)
            {
                await PopulateMailJobSelectsAsync(m.TemplateId, m.TemplateVersionId); // 驗證失敗也要重塞下拉
                return View(m);
            }

            // 1) 先存批次
            m.Status = "Scheduled";
            m.CreatedAt = DateTime.Now;                // 本地時間
            m.CreatedBy = User?.Identity?.Name ?? "system";
            m.TemplateKey = template.TemplateKey;
            m.CampaignName = m.CampaignName?.Trim();
            _db.MailJobs.Add(m);
            await _db.SaveChangesAsync();              // 拿到 m.JobId

			// 2) 展開名單 → MailJobRecipient（加驗證與去重）
			var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var recipients = new List<MailJobRecipient>();

			foreach (var line in (m.SegmentQuery ?? "")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
				var email = parts[0];
				var name = parts.Length > 1 ? parts[1] : null;

				if (string.IsNullOrWhiteSpace(email)) continue;
				if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) continue; // 驗證 email
				if (!emails.Add(email)) continue; // 去重

				recipients.Add(new MailJobRecipient
				{
					MailJobId = m.JobId,
					RecipientEmail = email,
					RecipientName = name,
					Status = "Pending"
				});
			}

			if (recipients.Count == 0)
			{
				ModelState.AddModelError(nameof(m.SegmentQuery), "名單為空或格式不正確");
				await PopulateMailJobSelectsAsync(m.TemplateId, m.TemplateVersionId);
				return View(m);
			}
            //上限50000避免名單過大卡死
			if (recipients.Count > 50000)
			{
				ModelState.AddModelError(nameof(m.SegmentQuery), "名單過大，請分批建立（上限 50,000）");
				await PopulateMailJobSelectsAsync(m.TemplateId, m.TemplateVersionId);
				return View(m);
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
			// 1) 讀取 Job（唯讀）
			var job = await _db.MailJobs
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.JobId == id);
			if (job == null) return NotFound();

			// 2) KPI：唯一統計改算在 MailJobRecipient（每收件者只算一次）
			var totalRecipients = await _db.MailJobRecipients
				.AsNoTracking()
				.CountAsync(r => r.MailJobId == id);

			var sentCount = await _db.MailJobRecipients
				.AsNoTracking()
				.CountAsync(r => r.MailJobId == id && r.Status == "Sent");

			var uniqueOpens = await _db.MailJobRecipients
				.AsNoTracking()
				.CountAsync(r => r.MailJobId == id && r.OpenCount > 0);

			var uniqueClicks = await _db.MailJobRecipients
				.AsNoTracking()
				.CountAsync(r => r.MailJobId == id && r.ClickCount > 0);

			// 3) 建 ViewModel（View 要這個型別）
			var vm = new MailJobDetailsVM
			{
				Job = job,
				TotalRecipients = totalRecipients,
				SentCount = sentCount,
				UniqueOpens = uniqueOpens,
				UniqueClicks = uniqueClicks
			};

			// 4) 列表資料給 ViewBag（保留你的原畫面；避免 int.MaxValue）
			ViewBag.Recipients = await _db.MailJobRecipients.AsNoTracking()
				.Where(x => x.MailJobId == id)
				.OrderBy(x => x.MailJobRecipientId)
				.Take(500) // 原本 int.MaxValue 太兇；先取 500 筆展示
				.ToListAsync();

			ViewBag.Logs = await _db.MailSendLogs.AsNoTracking()
				.Where(x => x.MailJobId == id && x.JobRecipientId != null)
				.OrderByDescending(x => x.LogId)
				.Take(200) // 原本 int.MaxValue；先取 200 筆
				.ToListAsync();

			ViewBag.Progress = $"{job.SentCount} / {job.TotalRecipients}";

			//寄信快照
			var sampleHtml = (ViewBag.Logs as List<BookLoop.Models.MailSendLog>)?
	.FirstOrDefault(l => !string.IsNullOrEmpty(l.BodySnapshot))?.BodySnapshot;

			if (string.IsNullOrEmpty(sampleHtml) && vm.Job.TemplateVersionId != null)
			{
				sampleHtml = await _db.TemplateVersions.AsNoTracking()
					.Where(v => v.TemplateVersionId == vm.Job.TemplateVersionId)
					.Select(v => v.BodyHtml) 
					.FirstOrDefaultAsync();
			}
			ViewBag.SampleHtml = sampleHtml;

			// 5) 回傳 VM
			return View(vm);
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

        // AJAX 動作 - 根據角色獲取會員 Email/Username
        [HttpGet]
        public async Task<IActionResult> GetMembersByGender(byte Gender)
        {
            var members = await _db.Members
                .AsNoTracking()
                .Where(m => m.Gender == Gender && !string.IsNullOrEmpty(m.Email)) // 確保有 Email
                .Select(m => new
                {
                    m.Email,
                    m.Username
                })
                .ToListAsync();

            return Json(members);
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

		[HttpGet]
		public async Task<IActionResult> MetricsAdaptive(long jobId)
		{
			// 1) 取 Job 與發送起點 t0（本地時間）
			var job = await _db.MailJobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == jobId);
			if (job == null) return NotFound();

			DateTime? t0 = job.StartedAt;
			if (t0 == null)
			{
				t0 = await _db.MailSendLogs
					.Where(l => l.MailJobId == jobId)
					.OrderBy(l => l.SentAt)
					.Select(l => (DateTime?)l.SentAt)
					.FirstOrDefaultAsync();
			}
			if (t0 == null) t0 = DateTime.Now.AddHours(-1); // 沒資料就給一個近端的預設視窗
			var start = t0.Value;                 // 本地時間
			var end = DateTime.Now;             // 本地時間

			// 2) 產生 buckets（分鐘/小時/日）
			var buckets = BuildBuckets(start, end); // 回傳 List<(DateTime from, DateTime to, string label)>

			// 3) 取出事件（一次撈完，避免多次 DB 往返）
			var events = await _db.MailEvents.AsNoTracking()
		.Where(e => e.MailJobId == jobId && e.CreatedAt >= start && e.CreatedAt <= end)
		.Select(e => new { e.EventType, e.CreatedAt, e.JobRecipientId })
		.OrderBy(e => e.CreatedAt) // ⬅️ 關鍵：先排序，等等用雙指標走桶
		.ToListAsync();

			// 4) 以「雙指標」塞桶（O(N + B)），取代 O(N×B) 的巢狀迴圈
			var opens = new int[buckets.Count];
			var clicks = new int[buckets.Count];

			// 先依收件者+事件類型去重
			var distinctEvents = events
				.Where(e => e.JobRecipientId != null)
				.GroupBy(e => new { e.EventType, e.JobRecipientId })
				.Select(g => g.OrderBy(x => x.CreatedAt).First()) // 取每位收件者最早一次開信/點擊
				.OrderBy(x => x.CreatedAt)
				.ToList();

			int bi = 0; // bucket index

			// 簡單掃描：資料量中小 OK；量大可改成二分搜尋或先依時間排序後單指標前進
			foreach (var ev in distinctEvents)
			{
				// 推進 bucket 指標到涵蓋 ev.CreatedAt 的位置
				// 由於 evs 依時間排序，bi 只會前進不會回退
				while (bi < buckets.Count && ev.CreatedAt >= buckets[bi].to) bi++;
				if (bi >= buckets.Count) break; 

				var b = buckets[bi];
				if (ev.CreatedAt >= b.from && ev.CreatedAt < b.to)
				{
					if (string.Equals(ev.EventType, "Open", StringComparison.OrdinalIgnoreCase)) opens[bi]++;
					else if (string.Equals(ev.EventType, "Click", StringComparison.OrdinalIgnoreCase)) clicks[bi]++;
				}
			}
			// 5) 邊界：若完全沒桶或沒事件，至少回一個點，避免前端圖表報錯
			if (buckets.Count == 0)
				buckets.Add((start, end, start.ToString("HH:mm")));

			return Json(new
			{
				labels = buckets.Select(b => b.label).ToArray(),
				opens,
				clicks,
				// 便利除錯資訊（可留可去）
				t0 = start.ToString("yyyy-MM-dd HH:mm:ss"),
				now = end.ToString("yyyy-MM-dd HH:mm:ss")
			});

			// ====== helper ======

			//依規則建 bucket（0–1 小時：每分鐘；1–48 小時：每小時；>48 小時：每天）
			static List<(DateTime from, DateTime to, string label)> BuildBuckets(DateTime t0, DateTime now)
			{
				var list = new List<(DateTime, DateTime, string)>();

				var oneHour = t0.AddHours(1);
				var fortyEight = t0.AddHours(48);

				// a) 前 1 小時（每分鐘）
				var endA = now < oneHour ? now : oneHour;
				var cur = t0;
				while (cur < endA)
				{
					var to = cur.AddMinutes(1);
					list.Add((cur, to, cur.ToString("HH:mm")));
					cur = to;
				}

				// b) 1–48 小時（每小時）
				if (now > oneHour)
				{
					var startB = oneHour;
					var endB = now < fortyEight ? now : fortyEight;
					cur = startB;
					while (cur < endB)
					{
						var to = cur.AddHours(1);
						list.Add((cur, to, cur.ToString("MM/dd HH:00")));
						cur = to;
					}
				}

				// c) 48 小時以後（每日）
				if (now > fortyEight)
				{
					// 從 t0 的第 3 天起，按日切（含不完整最後一天）
					var startC = new DateTime(fortyEight.Year, fortyEight.Month, fortyEight.Day, 0, 0, 0);
					cur = startC;
					while (cur < now)
					{
						var to = cur.Date.AddDays(1);
						var toClamped = to < now ? to : now;
						list.Add((cur, toClamped, cur.ToString("MM/dd")));
						cur = to;
					}
				}

				// 如果一筆都沒有（極端情況），至少給一格
				if (list.Count == 0)
					list.Add((t0, now, t0.ToString("HH:mm")));

				return list;
			}
		}




		//// 把未指定 Kind 的時間視為台北時間並轉換成 UTC；若已是 UTC 就直接回傳。
//		private static DateTime NormalizeToUtc(DateTime dt)
//        {
//            if (dt.Kind == DateTimeKind.Utc) return dt;

//            // 你的系統時區：Asia/Taipei
//            try
//            {
//                // Windows: "Taipei Standard Time"；Linux: "Asia/Taipei"
//                var tz = TimeZoneInfo.FindSystemTimeZoneById(
//#if WINDOWS
//                    "Taipei Standard Time"
//#else
//                    "Asia/Taipei"
//#endif
//                );
//                if (dt.Kind == DateTimeKind.Unspecified)
//                    return TimeZoneInfo.ConvertTimeToUtc(dt, tz);
//                if (dt.Kind == DateTimeKind.Local)
//                    return dt.ToUniversalTime();
//            }
//            catch
//            {
//                // 找不到時區設定就退而求其次：當成本地時間轉 UTC
//                return dt.ToUniversalTime();
//            }
//            return dt.ToUniversalTime();
//        }
    }
}
