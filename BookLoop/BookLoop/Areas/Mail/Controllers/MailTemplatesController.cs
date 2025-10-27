using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data;       // 引用 AppDbContext
using BookLoop.Models;    // 引用 MailTemplate
using Microsoft.AspNetCore.Authorization; // 引用 Authorize

namespace BookLoop.Areas.Mail.Controllers
{
    [Area("Mail")] 
    // [Authorize(Policy = "Mail.Templates.Manage")] 
    public class MailTemplatesController : Controller
    {
        private readonly AppDbContext _context;

        public MailTemplatesController(AppDbContext context)
        {
            _context = context;
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
            // 返回空的 View 以供使用者輸入
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
                TempData["ok"] = "郵件範本已建立。"; // (可選) 成功訊息
                return RedirectToAction(nameof(Index));
            }
            return View(mailTemplate); // 若驗證失敗，返回 View 顯示錯誤
        }

        // GET: Mail/MailTemplates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var mailTemplate = await _context.MailTemplates.FindAsync(id);
            if (mailTemplate == null) return NotFound();
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

        private bool MailTemplateExists(int id)
        {
            return _context.MailTemplates.Any(e => e.TemplateID == id);
        }
    }
}