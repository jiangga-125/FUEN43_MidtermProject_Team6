// Areas/Mail/Controllers/TemplateVersionsController.cs
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services.Mail;
using BookLoop.Services.Storage;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BookLoop.Areas.Mail.Controllers
{
	[Area("Mail")]
	public class TemplateVersionsController : Controller
	{
		private readonly AppDbContext _db;
		private readonly IWebHostEnvironment _env;
		private readonly IMailService _mail;
        private readonly ITemplateRenderer _renderer;
        private readonly ILogger<TemplateVersionsController> _logger;
        private readonly IFileStorage _storage;

        public TemplateVersionsController(AppDbContext db, IWebHostEnvironment env, IMailService mail, ITemplateRenderer renderer, ILogger<TemplateVersionsController> logger, IFileStorage storage)
        {
            _db = db;
            _env = env;
            _mail = mail;
            _renderer = renderer;
            _logger = logger;
            _storage = storage;
        }

        // 在 TemplateVersionsController.cs
        public async Task<IActionResult> Index(int templateId)
		{
			var t = await _db.Templates
				.Include(x => x.Versions) // 確保載入了 Versions
				.FirstOrDefaultAsync(x => x.TemplateId == templateId);

			if (t == null) return NotFound();

			return View(t); // Template 物件
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
		public async Task<IActionResult> Create([Bind("TemplateId,TemplateName,Subject,BodyHtml,DesignJson,IsActive,IsDefault")] TemplateVersion model) // 新簽章，明確 Bind
		{
			ModelState.Remove("Template");

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
            ModelState.Remove("Template");

            //檢查 TemplateName 唯一性
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

            if (string.IsNullOrWhiteSpace(m.TemplateName))
                ModelState.AddModelError(nameof(m.TemplateName), "請輸入版本名稱");

            if (!ModelState.IsValid)
            {
                // 返回 JSON 錯誤
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { ok = false, errors = errors });
            }

            //使用 Try...Catch 捕捉所有儲存錯誤
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

                await _db.SaveChangesAsync();

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
        }

        // POST: /Mail/TemplateVersions/Delete/5  （AJAX：回傳 JSON）
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.TemplateVersions.FirstOrDefaultAsync(v => v.TemplateVersionId == id);
            if (entity == null) return Json(new { ok = false, errors = new[] { "找不到指定的版本。" } });

            // 預設版不可刪除
            if (entity.IsDefault)
            {
                return Json(new
                {
                    ok = false,
                    errors = new[] { "此版本為預設版，無法刪除。請先在其他版本勾選為預設後再嘗試。" }
                });
            }

            try
            {
                int templateId = entity.TemplateId;

                _db.TemplateVersions.Remove(entity);
                await _db.SaveChangesAsync();

                var redirectUrl = Url.Action(nameof(Index), new { templateId });
                return Json(new { ok = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除 TemplateVersion (ID: {TemplateVersionId}) 失敗。", id);
                return Json(new { ok = false, errors = new[] { "刪除時發生錯誤。", ex.Message } });
            }
        }


        // POST: /Mail/TemplateVersions/TestSend?templateId=xx&templateVersionId=yy 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSend(int templateId, int? templateVersionId, [FromBody] TestSendDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.To))
                return BadRequest(new { ok = false, error = "缺少收件者" });

            // 1) 讀 Template（拿 TemplateKey）
            var template = await _db.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateId == templateId);
            if (template == null)
                return NotFound(new { ok = false, error = "找不到模板群組" });

            // 2) 若有指定 TemplateVersionId，就讀版本（拿 VersionId 與預設 Subject/Body）
            TemplateVersion? version = null;
            if (templateVersionId.HasValue)
            {
                version = await _db.TemplateVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.TemplateVersionId == templateVersionId.Value && v.TemplateId == templateId);
                if (version == null)
                    return NotFound(new { ok = false, error = "找不到指定的模板版本" });
            }
            else
            {
                version = await _db.TemplateVersions
                    .AsNoTracking()
                    .Where(v => v.TemplateId == templateId && v.IsActive)
                    .OrderByDescending(v => v.IsDefault).ThenByDescending(v => v.UpdatedAt)
                    .FirstOrDefaultAsync();
                // 沒有版本也允許試寄（只用前端傳來的 subject/body）
            }

            // 3) 建 token 並渲染（優先用前端送來的 Subject/Body；若沒帶就使用版本內容）
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Recipient"] = dto.To ?? "",
                ["Name"] = dto.Name ?? ""
                // 之後要加更多測試變數可在這裡擴充
            };

            var rawSubject = !string.IsNullOrWhiteSpace(dto.Subject) ? dto.Subject : (version?.Subject ?? "(無主旨)");
            var rawBody = !string.IsNullOrWhiteSpace(dto.BodyHtml) ? dto.BodyHtml : (version?.BodyHtml ?? string.Empty);

            string subject = _renderer.Render(rawSubject, tokens);
            string body = _renderer.Render(rawBody, tokens);

            try
            {
                // 4) ★ 用「新重載」把中繼資料一起傳下去，MailSendLogs 會寫齊
                await _mail.SendAsync(
                    to: dto.To,
                    subject: subject,
                    body: body,                                 // 這會寫到 BodySnapshot
                    attachmentName: null,
                    attachmentBytes: null,
                    contentType: "application/octet-stream",
                    templateId: template.TemplateId,
                    templateKey: template.TemplateKey,          // ← 需要 Template 有 TemplateKey 欄位
                    templateVersionId: version?.TemplateVersionId, // 沒指定版本就寫 null
                    mailJobId: null,                            // 試寄不是群發
                    category: "Test",                           // 試寄一律標記 "Test"
                    cancellationToken: default
                );

                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestSend 失敗 (TemplateId: {TemplateId}, TemplateVersionId: {TemplateVersionId})", templateId, templateVersionId);
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }
        public class TestSendDto
		{
			public string To { get; set; }
			public string? Subject { get; set; }
			public string? BodyHtml { get; set; }
			public string? Name { get; set; }
            public int? TemplateVersionId { get; set; }
        }
		// POST: /Mail/TemplateVersions/Upload?templateId=xx
		// Unlayer 圖片上傳端點
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upload(int templateId, IFormFile file, CancellationToken ct)
		{
            if (file == null || file.Length == 0) return BadRequest(new { error = "沒有檔案" });

            var ext = Path.GetExtension(file.FileName) ?? "";
            if (!Regex.IsMatch(ext, @"^\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase))
                return BadRequest(new { error = "不支援的檔案類型" });

            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var key = _storage.BuildKey("uploads", "mailtemplateimages", templateId.ToString(), today, name);

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? (ext.ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                })
                : file.ContentType;

            var url = await _storage.UploadAsync(key, bytes, contentType, ct);
            return Json(new { url }); // Unlayer 會把這個 src 直接放進 HTML
        }
	}
}
