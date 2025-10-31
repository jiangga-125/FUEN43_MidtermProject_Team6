// Areas/Mail/Controllers/TemplateVersionsController.cs
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Mail.Controllers
{
	[Area("Mail")]
	public class TemplateVersionsController : Controller
	{
		private readonly AppDbContext _db;
		private readonly IWebHostEnvironment _env;
		private readonly IMailService _mail;
        private readonly ILogger<TemplateVersionsController> _logger;

        public TemplateVersionsController(AppDbContext db, IWebHostEnvironment env, IMailService mail, ILogger<TemplateVersionsController> logger)
        {
            _db = db;
            _env = env;
            _mail = mail;
            _logger = logger;
        }

        // 在 TemplateVersionsController.cs
        public async Task<IActionResult> Index(int templateId)
		{
			var t = await _db.Templates
				.Include(x => x.Versions) // 確保載入了 Versions
				.FirstOrDefaultAsync(x => x.TemplateId == templateId);

			if (t == null) return NotFound();

			return View(t); // <-- ✅ 修正：直接傳遞 't' (Template 物件)
		}

		// GET: /Mail/TemplateVersions/Create?templateId=123
		[HttpGet]
		public async Task<IActionResult> Create(int templateId)
		{
			var template = await _db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.TemplateId == templateId);
			if (template == null) return NotFound();

			// 建立新版本的空白模型，TemplateId 帶入
			var vm = new TemplateVersion
			{
				TemplateId = templateId,
				IsActive = true
			};
			return View(vm);
		}

		// POST: /Mail/TemplateVersions/Create
		[HttpPost, ValidateAntiForgeryToken]
		// public async Task<IActionResult> Create(TemplateVersion model) // 舊簽章
		public async Task<IActionResult> Create([Bind("TemplateId,TemplateName,Subject,BodyHtml,DesignJson,IsActive,IsDefault")] TemplateVersion model) // 新簽章，明確 Bind
		{
			// === 修正 ===
			// 我們是透過 TemplateId 繫結，而非 Template 物件，
			// 所以要移除因導覽屬性為 null 而產生的驗證錯誤。
			ModelState.Remove("Template");
			// ============

			// 1) 基本驗證
			// 我們可以手動添加更多驗證
			if (string.IsNullOrWhiteSpace(model.TemplateName))
			{
				ModelState.AddModelError("TemplateName", "版本名稱為必填。");
			}

			if (!ModelState.IsValid)
			{
				// 如果是 AJAX，返回 JSON 錯誤
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
				return Json(new { ok = false, errors = errors });
				// return View(model); // 舊的返回
			}

			// 2) 保護：Template 必須存在
			var template = await _db.Templates.FirstOrDefaultAsync(t => t.TemplateId == model.TemplateId);
			if (template == null)
			{
				return Json(new { ok = false, errors = new[] { "所屬 Template 不存在。" } });
				// ModelState.AddModelError("", "所屬 Template 不存在。"); // 舊的
				// return View(model); // 舊的
			}

			// 3) IsDefault：確保每個 Template 只有一個預設版
			if (model.IsDefault)
			{
				var others = _db.TemplateVersions.Where(v => v.TemplateId == model.TemplateId && v.IsDefault);
				await others.ForEachAsync(v => v.IsDefault = false);
			}

			// 4) 儲存（DesignJson/Html 來自 Hidden 欄位）
			model.CreatedAt = DateTime.UtcNow;
			model.UpdatedAt = DateTime.UtcNow; // 確保 UpdatedAt 也有值
			_db.TemplateVersions.Add(model);
			await _db.SaveChangesAsync();

			// 5) 返回 JSON 成功訊息，並附上重導向 URL
			var redirectUrl = Url.Action("Index", "TemplateVersions", new { area = "Mail", templateId = model.TemplateId });
			return Json(new { ok = true, redirectUrl = redirectUrl });

			// return RedirectToAction("Index", "TemplateVersions", new { area = "Mail", templateId = model.TemplateId }); // 舊的返回
		}
		// GET: /Mail/TemplateVersions/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			var m = await _db.TemplateVersions.FindAsync(id);
			if (m == null) return NotFound();

			return View(m);
		}

        // POST: /Mail/TemplateVersions/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("TemplateVersionId,TemplateId,TemplateName,Subject,BodyHtml,DesignJson,IsActive,IsDefault")] TemplateVersion m)
        {
            // 移除因導覽屬性為 null 產生的驗證錯誤
            ModelState.Remove("Template");

            // === 新增：檢查 TemplateName 唯一性 ===
            if (!string.IsNullOrWhiteSpace(m.TemplateName))
            {
                // 檢查在同一個 TemplateId 下，是否有 *其他* 版本使用了相同的名稱
                bool nameExists = await _db.TemplateVersions.AnyAsync(v =>
                    v.TemplateId == m.TemplateId && // 屬於同一個範本
                    v.TemplateVersionId != m.TemplateVersionId && // 且不是自己
                    v.TemplateName == m.TemplateName); // 名稱相同

                if (nameExists)
                {
                    ModelState.AddModelError(nameof(m.TemplateName), "此版本名稱已被使用，請更換。");
                }
            }
            // ======================================

            if (string.IsNullOrWhiteSpace(m.TemplateName))
                ModelState.AddModelError(nameof(m.TemplateName), "請輸入版本名稱");

            if (!ModelState.IsValid)
            {
                // 返回 JSON 錯誤
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { ok = false, errors = errors });
            }

            // === 新增：使用 Try...Catch 捕捉所有儲存錯誤 ===
            try
            {
                var entity = await _db.TemplateVersions.FindAsync(m.TemplateVersionId);
                if (entity == null)
                {
                    return Json(new { ok = false, errors = new[] { "找不到指定的版本。" } });
                }

                // 更新欄位
                entity.TemplateName = m.TemplateName;
                entity.Subject = m.Subject;
                entity.BodyHtml = m.BodyHtml;
                entity.DesignJson = m.DesignJson;
                entity.IsActive = m.IsActive;
                entity.IsDefault = m.IsDefault;
                entity.UpdatedAt = DateTime.UtcNow;

                // 若本版本設為預設，把其他版本的 IsDefault 清掉
                if (entity.IsDefault)
                {
                    var others = await _db.TemplateVersions
                        .Where(v => v.TemplateId == entity.TemplateId && v.TemplateVersionId != entity.TemplateVersionId && v.IsDefault)
                        .ToListAsync();
                    foreach (var v in others) v.IsDefault = false;
                }

                await _db.SaveChangesAsync(); // 👈 這裡是潛在的錯誤點

                // 返回 JSON 成功訊息
                var redirectUrl = Url.Action(nameof(Index), new { templateId = entity.TemplateId });
                return Json(new { ok = true, redirectUrl = redirectUrl });
            }
            catch (Exception ex)
            {
                // 捕捉所有例外 (包含 DbUpdateException)，並回傳 JSON 錯誤
                _logger.LogError(ex, "儲存 TemplateVersion (ID: {TemplateVersionId}) 時發生錯誤。", m.TemplateVersionId);
                return Json(new
                {
                    ok = false,
                    errors = new[] { "儲存時發生資料庫錯誤，請稍後再試。", ex.Message }
                });
            }
            // ======================================
        }

        // POST: /Mail/TemplateVersions/TestSend?templateId=xx
        // 由前端送 JSON: { to, subject, bodyHtml, name }
        [HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> TestSend(int templateId, [FromBody] TestSendDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.To))
				return BadRequest(new { ok = false, error = "缺少收件者" });

			var t = await _db.Templates.FindAsync(templateId);
			if (t == null) return NotFound(new { ok = false, error = "找不到模板群組" });

			// 簡單 Token 替換（你若有 ITemplateRenderer，可改成 _renderer.Render(...)）
			string body = dto.BodyHtml ?? string.Empty;
			body = body.Replace("{{Recipient}}", dto.To ?? "", StringComparison.OrdinalIgnoreCase)
					   .Replace("{{Name}}", dto.Name ?? "", StringComparison.OrdinalIgnoreCase);

			try
			{
				await _mail.SendAsync(dto.To, dto.Subject ?? "(無主旨)", body);
				return Json(new { ok = true });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { ok = false, error = ex.Message });
			}
		}

		public class TestSendDto
		{
			public string To { get; set; }
			public string Subject { get; set; }
			public string BodyHtml { get; set; }
			public string Name { get; set; }
		}
		// POST: /Mail/TemplateVersions/Upload?templateId=xx
		// Unlayer 圖片上傳端點
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upload(int templateId, IFormFile file)
		{
			if (file == null || file.Length == 0)
				return BadRequest(new { error = "沒有檔案" });

			// 目錄：/wwwroot/uploads/mailtemplateimages/{templateId}/yyyyMMdd/
			var today = DateTime.UtcNow.ToString("yyyyMMdd");
			var relDir = Path.Combine("uploads", "mailtemplateimages", templateId.ToString(), today);
			var absDir = Path.Combine(_env.WebRootPath ?? "wwwroot", relDir);
			Directory.CreateDirectory(absDir);

			// 檔名：yyyyMMddHHmmssfff + 原副檔名
			var ext = Path.GetExtension(file.FileName);
			// 稍微加強副檔名的安全性檢查
			var safeExt = Regex.IsMatch(ext, @"^\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase)
				? ext.ToLowerInvariant()
				: ".bin";

			if (safeExt == ".bin")
				return BadRequest(new { error = "不支援的檔案類型" });

			var name = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + safeExt;

			var absPath = Path.Combine(absDir, name);
			using (var fs = System.IO.File.Create(absPath))
			{
				await file.CopyToAsync(fs);
			}

			// === 修正：產生絕對 URL ===

			// 1. 取得網站的 Base URL (例如：https://localhost:7123)
			var baseUrl = $"{Request.Scheme}://{Request.Host}";

			// 2. 組合相對路徑 (例如：/uploads/mailtemplateimages/1/20251030/image.jpg)
			var relativeUrl = "/" + Path.Combine(relDir, name).Replace("\\", "/");

			// 3. 組合為絕對 URL
			var absoluteUrl = baseUrl + relativeUrl;

			// 舊的相對 URL:
			// var url = "/" + Path.Combine(relDir, name).Replace("\\", "/");

			// 返回絕對 URL 給 Unlayer
			return Json(new { url = absoluteUrl });
		}
	}
}
