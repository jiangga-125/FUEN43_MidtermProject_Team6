using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Mail.Controllers
{
	[Area("Mail")]
	public class TemplatesController : Controller
	{
		private readonly AppDbContext _db;
		public TemplatesController(AppDbContext db) => _db = db;

		// GET: /Mail/Templates
		public async Task<IActionResult> Index()
		{
			var list = await _db.Templates
				.Include(t => t.Versions)
				.OrderBy(t => t.TemplateKey)
				.ToListAsync();

			return View(list); 
		}

		// GET: /Mail/Templates/Create
		public IActionResult Create() => View();

		// POST: /Mail/Templates/Create
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("TemplateKey,Description")] Template m)
		{
			if (await _db.Templates.AnyAsync(x => x.TemplateKey == m.TemplateKey))
				ModelState.AddModelError(nameof(m.TemplateKey), "此 TemplateKey 已存在");
			if (!ModelState.IsValid) return View(m);

			_db.Templates.Add(m);
			await _db.SaveChangesAsync();
			return RedirectToAction("Index");
		}

        // GET: /Mail/Templates/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var t = await _db.Templates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TemplateId == id);
            if (t == null) return NotFound();
            return View(t);
        }

        // POST: /Mail/Templates/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TemplateId,TemplateKey,Description")] Template m)
        {
            if (id != m.TemplateId) return BadRequest();

            // TemplateKey 唯一性（排除自己）
            bool keyExists = await _db.Templates
                .AnyAsync(x => x.TemplateKey == m.TemplateKey && x.TemplateId != m.TemplateId);
            if (keyExists)
                ModelState.AddModelError(nameof(m.TemplateKey), "此 TemplateKey 已被使用");

            if (!ModelState.IsValid) return View(m);

            var entity = await _db.Templates.FirstOrDefaultAsync(x => x.TemplateId == id);
            if (entity == null) return NotFound();

            entity.TemplateKey = m.TemplateKey?.Trim() ?? "";
            entity.Description = m.Description ?? "";

            try
            {
                await _db.SaveChangesAsync();
                TempData["Ok"] = "已更新範本。";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                TempData["Err"] = $"更新失敗：{ex.Message}";
                return View(m);
            }
        }

        // GET: /Mail/Templates/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var t = await _db.Templates
                .Include(x => x.Versions)
                .FirstOrDefaultAsync(x => x.TemplateId == id);
            if (t == null) return NotFound();
            return View(t);
        }

        // POST: /Mail/Templates/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var t = await _db.Templates
                .Include(x => x.Versions)
                .FirstOrDefaultAsync(x => x.TemplateId == id);
            if (t == null) return NotFound();
            // 若群組內還有預設版本 → 阻擋刪除
            if (t.Versions.Any(v => v.IsDefault))
            {
                TempData["Err"] = "此範本群組仍有『預設版本』，請先取消所有預設或改為非預設後再刪除。";
                // 回到該群組的版本列表頁（你這頁）
                return RedirectToAction("Index", "TemplateVersions", new { area = "Mail", templateId = id });
            }
            try
            {
                // 一次刪乾淨：先刪版本，再刪 Template
                _db.TemplateVersions.RemoveRange(t.Versions);
                _db.Templates.Remove(t);
                await _db.SaveChangesAsync();

                TempData["Ok"] = "已刪除範本與其所有版本。";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                TempData["Err"] = $"刪除失敗：{ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }

}
