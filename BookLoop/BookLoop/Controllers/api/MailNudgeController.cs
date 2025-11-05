using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BookLoop.Controllers.api
{
	[Route("api/mail")]
	[Authorize] // 使用 JWT
	[ApiController]
	public class MailNudgeController : ControllerBase
	{
		private readonly AppDbContext _db;
		private readonly IConfiguration _cfg;

		public MailNudgeController(AppDbContext db, IConfiguration cfg)
		{
			_db = db;
			_cfg = cfg;
		}

		// =======================
		// 取得會員未開信提示
		// =======================
		[HttpGet("unopened")]
		public async Task<IActionResult> GetUnopened()
		{
			// 1. 從 JWT claims 取得 Email（或由 mid 反查 Members 表）
			var email = await GetCurrentMemberEmailAsync();
			if (string.IsNullOrWhiteSpace(email))
				return Ok(new { hasItem = false });

			// 2. 找出該會員尚未開啟的最新信件
			var r = await _db.MailJobRecipients.AsNoTracking()
				.Where(x => x.RecipientEmail == email && x.Status == "Sent" && x.OpenCount == 0)
				.OrderByDescending(x => x.SentAt)
				.Select(x => new { x.MailJobRecipientId, x.MailJobId, x.RecipientName })
				.FirstOrDefaultAsync();

			if (r == null) return Ok(new { hasItem = false });

			// 3) 產生簽章避免 URL 被猜
			var secret = _cfg["Mail:ViewSecret"];
			if (string.IsNullOrWhiteSpace(secret))
			{
				// 讓你一眼看出是設定沒設好
				return StatusCode(500, new { error = "Mail:ViewSecret is missing" });
			}
			var token = Sign($"{r.MailJobRecipientId}", _cfg["Mail:ViewSecret"]);

			// 4. 抓寄送快照
			var log = await _db.MailSendLogs.AsNoTracking()
				.Where(l => l.JobRecipientId == r.MailJobRecipientId)
				.OrderByDescending(l => l.LogId)
				.Select(l => new { l.Subject, l.BodySnapshot })
				.FirstOrDefaultAsync();

			var subject = log?.Subject ?? "您有一封未讀的訊息";
			var preview = MakePreview(log?.BodySnapshot);

			// 直接手動組，避免 Url.Action 在屬性路由下漏掉 query 參數
			var viewUrl = $"{Request.Scheme}://{Request.Host}/api/mail/view/{r.MailJobRecipientId}?s={token}";

			return Ok(new
			{
				hasItem = true,
				subject,
				preview,
				name = r.RecipientName,
				viewUrl,
				rid = r.MailJobRecipientId
			});
		}

		// =======================
		// 開啟郵件內容（允許匿名）
		// =======================
		[AllowAnonymous]
		[HttpGet("view/{id:long}")]
		public async Task<IActionResult> ViewMessage(long id, [FromQuery] string s)
		{
			if (!Verify($"{id}", s, _cfg["Mail:ViewSecret"]))
				return Unauthorized();

			// 取快照 HTML
			var html = await _db.MailSendLogs.AsNoTracking()
				.Where(l => l.JobRecipientId == id && l.BodySnapshot != null)
				.OrderByDescending(l => l.LogId)
				.Select(l => l.BodySnapshot!)
				.FirstOrDefaultAsync();

			if (string.IsNullOrEmpty(html))
			{
				html = await _db.MailJobRecipients.AsNoTracking()
					.Where(r => r.MailJobRecipientId == id)
					.Join(_db.MailJobs.AsNoTracking(),
						  r => r.MailJobId, j => j.JobId,
						  (r, j) => j.TemplateVersionId)
					.Join(_db.TemplateVersions.AsNoTracking(),
						  tvid => tvid, tv => tv.TemplateVersionId,
						  (tvid, tv) => tv.BodyHtml)
					.FirstOrDefaultAsync()
					?? "<p>找不到可顯示的內容</p>";
			}

			// 標記開信（只記第一次）
			var rec = await _db.MailJobRecipients.FirstOrDefaultAsync(r => r.MailJobRecipientId == id);
			if (rec != null && rec.OpenCount == 0)
			{
				rec.OpenCount = 1;
				rec.OpenedAt = DateTime.Now;
				await _db.SaveChangesAsync();
			}

			return Content(html, "text/html; charset=utf-8");
		}

		// =======================
		// Private helpers
		// =======================

		/// <summary>
		/// 從 JWT 取 Email；若無則用 mid 查 Members 表
		/// </summary>
		private async Task<string?> GetCurrentMemberEmailAsync()
		{
			// 你的 Token 裡 email 是「http://schemas.../emailaddress」
			var email = User.FindFirst(ClaimTypes.Email)?.Value;
			if (!string.IsNullOrWhiteSpace(email))
				return email;

			// 你的 nameidentifier 也放 email，可備援
			email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!string.IsNullOrWhiteSpace(email))
				return email;

			// 自訂 claim：mid（會員 ID）
			var midStr = User.FindFirst("mid")?.Value;
			if (int.TryParse(midStr, out var memberId))
			{
				email = await _db.Members.AsNoTracking()
					.Where(m => m.MemberID == memberId)
					.Select(m => m.Email)
					.FirstOrDefaultAsync();

				if (!string.IsNullOrWhiteSpace(email))
					return email;
			}

			return null;
		}

		private static string Sign(string payload, string? secret)
		{
			if (string.IsNullOrEmpty(secret)) return "";
			using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
			var hash = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
			return Convert.ToHexString(hash);
		}

		private static bool Verify(string payload, string given, string? secret) =>
			string.Equals(Sign(payload, secret), given, StringComparison.OrdinalIgnoreCase);

		private static string MakePreview(string? html, int maxLen = 50)
		{
			if (string.IsNullOrEmpty(html)) return "";
			html = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "");
			html = System.Net.WebUtility.HtmlDecode(html).Trim();
			return html.Length > maxLen ? html[..maxLen] + "…" : html;
		}
	}
}
