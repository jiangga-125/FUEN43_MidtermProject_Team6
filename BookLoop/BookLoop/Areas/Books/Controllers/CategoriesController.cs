using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class CategoriesController : Controller
	{
		private readonly BookSystemContext _context; // 資料庫存取用的 DbContext

		// 建構子：注入 DbContext
		public CategoriesController(BookSystemContext context)
		{
			_context = context;
		}

		#region 列表(Index)

		// GET: /Books/Categories
		// 顯示分類清單
		public async Task<IActionResult> Index()
		{
			var list = await _context.Categories.ToListAsync();
			return View(list);
		}

		#endregion

		#region 新增(Create)

		// GET: /Books/Categories/Create
		// 顯示「新增分類」的表單頁面
		public IActionResult Create()
		{
			return View();
		}

		// POST: /Books/Categories/Create
		// 儲存新分類資料
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Category category)
		{
			category.Slug = SlugHelper.Generate(category.CategoryName);
			ModelState.Remove("Slug"); // 保險

			if (!ModelState.IsValid) return View(category);

			if (_context.Categories.Any(c => c.CategoryName == category.CategoryName))
			{
				ModelState.AddModelError("CategoryName", "此分類已存在！");
				return View(category);
			}

			category.CreatedAt = DateTime.Now;
			category.UpdatedAt = DateTime.Now;
			category.IsDeleted = false;

			_context.Add(category);
			await _context.SaveChangesAsync();

			TempData["Success"] = "分類新增成功！";//新增完成訊息

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 修改(Edit)

		// GET: /Books/Categories/Edit/5
		// 依照 CategoryID 把資料抓出來，顯示在編輯表單
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FindAsync(id);
			if (category == null) return NotFound();

			return View(category);
		}

		// POST: /Books/Categories/Edit/5
		// 更新分類內容
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Category category)
		{
			if (id != category.CategoryID) return NotFound();

			category.Slug = SlugHelper.Generate(category.CategoryName);
			ModelState.Remove("Slug");

			if (!ModelState.IsValid) return View(category);

			if (_context.Categories.Any(c => c.CategoryName == category.CategoryName && c.CategoryID != category.CategoryID))
			{
				ModelState.AddModelError("CategoryName", "此分類已存在！");
				return View(category);
			}

			category.UpdatedAt = DateTime.Now;
			_context.Update(category);
			await _context.SaveChangesAsync();

			TempData["Success"] = "分類修改成功！";//修改完成訊息

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 刪除(Delete)

		// GET: /Books/Categories/Delete/5
		// 顯示刪除確認頁面
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
			if (category == null) return NotFound();

			return View(category);
		}

		// POST: /Books/Categories/Delete/5
		// 確認刪除分類
		[HttpPost, ActionName("Delete")] // 與 GET 共用 URL
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var category = await _context.Categories.FindAsync(id);
			if(category != null)

			{
				_context.Categories.Remove(category);
				await _context.SaveChangesAsync();

				TempData["Success"] = "分類刪除成功！"; // 刪除完成訊息
			}

		else
			{
				TempData["Error"] = "刪除失敗，分類不存在！"; // 刪除失敗訊息
			}
			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 詳細資訊(Details)

		// GET: /Books/Categories/Details/5
		// 顯示單一分類的完整資訊
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
			if (category == null) return NotFound();

			return View(category);
		}

		#endregion
	}
}
