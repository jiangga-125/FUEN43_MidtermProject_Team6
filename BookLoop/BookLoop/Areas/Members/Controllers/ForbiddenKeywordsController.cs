using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Members.Controllers
{
	[Area("Members")]
	public class ForbiddenKeywordsController : Controller
	{
		private readonly MemberContext _db;
		public ForbiddenKeywordsController(MemberContext db) => _db = db;

		public async Task<IActionResult> Index()
		{
			var keywords = await _db.ReviewForbiddenKeyword.ToListAsync();
			return View(keywords);
		}

		public IActionResult Create() => View();

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(ReviewForbiddenKeyword kw)
		{
			if (!ModelState.IsValid) return View(kw);
			_db.ReviewForbiddenKeyword.Add(kw);
			await _db.SaveChangesAsync();
			return RedirectToAction(nameof(Index));
		}

		public async Task<IActionResult> Edit(int id)
		{
			var kw = await _db.ReviewForbiddenKeyword.FindAsync(id);
			if (kw == null) return NotFound();
			return View(kw);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(ReviewForbiddenKeyword kw)
		{
			if (!ModelState.IsValid) return View(kw);
			kw.UpdatedAt = DateTime.Now;
			_db.Update(kw);
			await _db.SaveChangesAsync();
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id)
		{
			var kw = await _db.ReviewForbiddenKeyword.FindAsync(id);
			if (kw != null)
			{
				_db.Remove(kw);
				await _db.SaveChangesAsync();
			}
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Toggle(int id)
		{
			var kw = await _db.ReviewForbiddenKeyword.FindAsync(id);
			if (kw == null) return NotFound();

			// 反轉狀態
			kw.IsActive = !kw.IsActive;
			kw.UpdatedAt = DateTime.Now;

			await _db.SaveChangesAsync();

			TempData["Msg"] = kw.IsActive
				? $"已啟用禁用詞：{kw.Keyword}"
				: $"已停用禁用詞：{kw.Keyword}";

			return RedirectToAction(nameof(Index));
		}

	}
}
	
