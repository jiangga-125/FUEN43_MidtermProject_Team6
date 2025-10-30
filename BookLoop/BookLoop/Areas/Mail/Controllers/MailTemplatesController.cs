using BookLoop.Data;       // 引用 AppDbContext
using BookLoop.Models;    // 引用 MailTemplate
using Microsoft.AspNetCore.Authorization; // 引用 Authorize
using Microsoft.AspNetCore.Hosting; //取得 wwwroot 路徑
using Microsoft.AspNetCore.Http; // 使用 IFormFile
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering; // SelectListItem
using System;
using System.IO; // 檔案操作
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Services.Mail;    
using System.Text.Json;          

namespace BookLoop.Areas.Mail.Controllers
{
    [Area("Mail")] 
    // [Authorize(Policy = "Mail.Templates.Manage")] 
    public class MailTemplatesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
		private readonly IMailService _mail;
		private readonly ITemplateRenderer _renderer;

		public MailTemplatesController(AppDbContext context, IWebHostEnvironment hostEnvironment, IMailService mail, ITemplateRenderer renderer)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
			_mail = mail;           
			_renderer = renderer;
		}

        // GET: Mail/MailTemplates
        public async Task<IActionResult> Index()
        {
            // 基礎的列表查詢
            return View(await _context.MailTemplates.OrderBy(t => t.TemplateKey).ToListAsync());
        }

        // GET: Mail/MailTemplates/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var mailTemplate = await _context.MailTemplates
                .FirstOrDefaultAsync(m => m.TemplateID == id);
            if (mailTemplate == null) return NotFound();
            return View(mailTemplate);
        }

        // GET: Mail/MailTemplates/Create
        public IActionResult Create()
        {
            PopulateTemplateKeysDropdown();
            return View(new MailTemplate { IsActive = true }); // 預設啟用
        }

        // POST: Mail/MailTemplates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TemplateKey,Subject,BodyHtml,Description,IsActive")] MailTemplate mailTemplate)
        {
            // 移除 ModelState 中由資料庫生成的欄位，避免驗證錯誤
            ModelState.Remove("TemplateID");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            // 檢查 TemplateKey 是否重複 (因為資料庫有唯一約束)
            if (await _context.MailTemplates.AnyAsync(t => t.TemplateKey == mailTemplate.TemplateKey))
            {
                ModelState.AddModelError("TemplateKey", "範本識別碼 (Key) 已存在。");
            }


            if (ModelState.IsValid)
            {
                // CreatedAt 和 UpdatedAt 由資料庫預設值或 Trigger 處理
                // mailTemplate.CreatedAt = DateTime.UtcNow; // 不需要
                // mailTemplate.UpdatedAt = DateTime.UtcNow; // 不需要 (或由 Trigger 處理)
                _context.Add(mailTemplate);
                await _context.SaveChangesAsync();
                TempData["ok"] = "郵件範本已建立。"; //成功訊息
                return RedirectToAction(nameof(Index));
            }
            PopulateTemplateKeysDropdown(mailTemplate.TemplateKey);
            return View(mailTemplate); // 若驗證失敗，返回 View 顯示錯誤
        }

        // GET: Mail/MailTemplates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var mailTemplate = await _context.MailTemplates.FindAsync(id);
            if (mailTemplate == null) return NotFound();
            PopulateTemplateKeysDropdown(mailTemplate.TemplateKey);
            return View(mailTemplate);
        }

        // POST: Mail/MailTemplates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TemplateID,TemplateKey,Subject,BodyHtml,Description,IsActive")] MailTemplate mailTemplate)
        {
            if (id != mailTemplate.TemplateID) return NotFound();

            // 移除 ModelState 中由資料庫生成的欄位
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            // 檢查 TemplateKey 是否與其他記錄重複
            if (await _context.MailTemplates.AnyAsync(t => t.TemplateKey == mailTemplate.TemplateKey && t.TemplateID != id))
            {
                ModelState.AddModelError("TemplateKey", "範本識別碼 (Key) 已存在。");
            }


            if (ModelState.IsValid)
            {
                try
                {
                    // 手動更新 UpdatedAt
                    var entry = _context.Entry(mailTemplate);
                    entry.State = EntityState.Modified;
                    entry.Property(x => x.CreatedAt).IsModified = false; // 不要更新 CreatedAt
                    entry.Property(x => x.UpdatedAt).CurrentValue = DateTime.UtcNow; // 手動設定 UpdatedAt

                    // 如果您有 Trigger，EF Core 會自動讀取更新後的值
                    //_context.Update(mailTemplate);
                    // 告訴 EF Core 不要更新 CreatedAt (因為它是 Identity)
                    //_context.Entry(mailTemplate).Property(x => x.CreatedAt).IsModified = false;
                    // UpdatedAt 由 Trigger 更新，EF Core 會在 SaveChanges 後讀取

                    await _context.SaveChangesAsync();
                    TempData["ok"] = "郵件範本已更新。"; // 成功訊息
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MailTemplateExists(mailTemplate.TemplateID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PopulateTemplateKeysDropdown(mailTemplate.TemplateKey);
            return View(mailTemplate); // 若驗證失敗，返回 View 顯示錯誤
        }

        // GET: Mail/MailTemplates/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var mailTemplate = await _context.MailTemplates
                .FirstOrDefaultAsync(m => m.TemplateID == id);
            if (mailTemplate == null) return NotFound();
            return View(mailTemplate);
        }

        // POST: Mail/MailTemplates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mailTemplate = await _context.MailTemplates.FindAsync(id);
            if (mailTemplate != null)
            {
                _context.MailTemplates.Remove(mailTemplate);
                await _context.SaveChangesAsync();
                TempData["ok"] = "郵件範本已刪除。"; // 成功訊息
            }
            return RedirectToAction(nameof(Index));
        }

		// === 預覽 ===
		// GET: Mail/MailTemplates/Preview/5?recipient=...&Name=...（Query 會當作模板變數）
		[HttpGet]
		public async Task<IActionResult> Preview(int? id)
		{
			if (id == null) return NotFound();

			var tpl = await _context.MailTemplates.FirstOrDefaultAsync(x => x.TemplateID == id);
			if (tpl == null) return NotFound();

			// 將 QueryString 轉為模板變數字典
			var model = HttpContext.Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

			// 渲染主旨與內容（※ 你的欄位名是 Subject / BodyHtml）
			var subject = _renderer.Render(tpl.Subject ?? "", model);
			var html = _renderer.Render(tpl.BodyHtml ?? "", model);

			ViewBag.TemplateId = tpl.TemplateID;
			ViewBag.Subject = subject;
			ViewBag.RawHtml = html;
			return View(); // 走 Views/MailTemplates/Preview.cshtml
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult RenderPreview([FromBody] PreviewInput input)
		{
			// 將前端傳來的 tokenJson 轉為字典
			var model = ParseJson(input.TokenJson);
			if (!string.IsNullOrWhiteSpace(input.Recipient))
				model["Recipient"] = input.Recipient;
			if (!string.IsNullOrWhiteSpace(input.Name))
				model["Name"] = input.Name;

			var subject = _renderer.Render(input.Subject ?? string.Empty, model);
			var html = _renderer.Render(input.BodyHtml ?? string.Empty, model);

			return Json(new { subject, html });
		}

		// === 試寄（右側按鈕用；前端用 fetch 傳 JSON）===
		// POST: Mail/MailTemplates/TestSend
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> TestSend([FromBody] TestSendDto dto, CancellationToken ct = default)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.To))
				return BadRequest("To is required.");

			// 以 Recipient / Name 為唯一可用 token（已拿掉 JSON 變數）
			var model = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Recipient"] = dto.To
			};
			if (!string.IsNullOrWhiteSpace(dto.Name))
				model["Name"] = dto.Name!;

			// 與實際寄送一致：用同一個 renderer 做 token 替換
			var subject = _renderer.Render(dto.Subject ?? "", model);
			var html = _renderer.Render(dto.BodyHtml ?? "", model);

			await _mail.SendAsync(dto.To, subject, html, ct);
			return Json(new { ok = true });
		}

		// 圖片上傳 Action
		[HttpPost]
        [ValidateAntiForgeryToken] // 建議加上 CSRF 保護
        // [Authorize(Policy = "Mail.Templates.Manage")] 
        public async Task<IActionResult> UploadImage(IFormFile upload)
        {
            if (upload == null || upload.Length == 0)
            {
                return BadRequest(new { error = new { message = "未選擇檔案或檔案為空。" } });
            }

            // 檔案驗證
            var permittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
            {
                return BadRequest(new { error = new { message = "不支援的檔案類型。" } });
            }
            if (upload.Length > 5 * 1024 * 1024) // 限制大小 (例如 5MB)
            {
                return BadRequest(new { error = new { message = "檔案大小超過限制 (5MB)。" } });
            }
            // --- 驗證結束 ---


            // --- 儲存檔案 ---
            // 決定儲存路徑 (例如：wwwroot/uploads/mailtemplateimages/)
            var uploadsFolderPath = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "mailtemplateimages");
            // 確保資料夾存在
            if (!Directory.Exists(uploadsFolderPath))
            {
                Directory.CreateDirectory(uploadsFolderPath);
            }

            // 產生唯一檔名 (避免覆蓋)
            var uniqueFileName = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await upload.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                // 處理檔案儲存錯誤
                return StatusCode(500, new { error = new { message = $"檔案儲存失敗: {ex.Message}" } });
            }

            // 返回 CKEditor 需要的 JSON 格式
            // 計算公開可訪問的 URL
            var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/mailtemplateimages/{uniqueFileName}";

            return Ok(new { url = imageUrl }); //CKEditor SimpleUploadAdapter 需要 "url" 屬性
        }

        private bool MailTemplateExists(int id)
        {
            return _context.MailTemplates.Any(e => e.TemplateID == id);
        }

        // --- 輔助方法：建立 TemplateKey 下拉選單 ---
        private void PopulateTemplateKeysDropdown(string? selectedKey = null)
        {
            // 定義系統預期使用的固定 Key 列表
            var predefinedKeys = new List<string> {
            "NewMemberWelcome",
            "OrderConfirmation",
            "OrderShipped",
            "OrderDelivered", // 假設有
            "ReservationAvailable",
            "BorrowOverdueNotice",
            "ReportExportNotification",
            "PasswordResetRequest" // 假設有
            // --- 您可以根據實際需要增減 ---
        };

            ViewBag.TemplateKeyList = predefinedKeys.Select(key => new SelectListItem
            {
                Text = key, // 可以考慮給中文名稱，但 Value 必須是 Key
                Value = key,
                Selected = key == selectedKey
            }).ToList();
        }

		// 供 RenderPreview / TestSend 共用的小工具，解析 JSON 變數
		private static Dictionary<string, string> ParseJson(string? json)
		{
			if (string.IsNullOrWhiteSpace(json)) return new();
			try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json!) ?? new(); }
			catch { return new(); }
		}

		public sealed class PreviewInput
		{
			public string? Subject { get; set; }
			public string? BodyHtml { get; set; }
			public string? TokenJson { get; set; }
			public string? Recipient { get; set; }
			public string? Name { get; set; }
		}

		public sealed class TestSendDto
		{
			public string To { get; set; } = "";
			public string? Name { get; set; }
			public string? Subject { get; set; }
			public string? BodyHtml { get; set; }
		}

	}
}