using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Helpers;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class PublishersController : Controller
	{
		private readonly BookSystemContext _context; // 資料庫存取用的 DbContext

		// 建構子：注入 DbContext
		public PublishersController(BookSystemContext context)
		{
			_context = context;
		}

		#region 列表(Index)

		// GET: /Books/Publishers
		// 顯示出版社清單
		public async Task<IActionResult> Index()
		{
			var list = await _context.Publishers.ToListAsync();
			return View(list);
		}

		#endregion

		#region 新增(Create)

		// GET: /Books/Publishers/Create
		// 顯示「新增出版社」的表單頁面
		public IActionResult Create()
		{
			return View();
		}

		// POST: /Books/Publishers/Create
		// 當使用者在表單按下「儲存」後，會進到這裡
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Publisher publisher)
		{
			publisher.Slug = BookLoop.Helpers.SlugHelper.Generate(publisher.PublisherName);

			if (!ModelState.IsValid) return View(publisher);

			if (_context.Publishers.Any(p => p.PublisherName == publisher.PublisherName))
			{
				ModelState.AddModelError("PublisherName", "此出版社已存在！");
				return View(publisher);
			}

			publisher.CreatedAt = DateTime.Now;
			publisher.UpdatedAt = DateTime.Now;
			publisher.IsDeleted = false;

			_context.Add(publisher);
			await _context.SaveChangesAsync();

			TempData["Success"] = "出版社新增成功！"; // 新增完成訊息

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 修改(Edit)

		// GET: /Books/Publishers/Edit/5
		// 依照 PublisherID 把資料抓出來，顯示在編輯表單
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FindAsync(id);
			if (publisher == null) return NotFound();

			return View(publisher);
		}

		// POST: /Books/Publishers/Edit/5
		// 更新出版社內容
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Publisher publisher)
		{
			if (id != publisher.PublisherID) return NotFound();
			publisher.Slug = BookLoop.Helpers.SlugHelper.Generate(publisher.PublisherName);
			if (!ModelState.IsValid) return View(publisher);

			if (_context.Publishers.Any(p => p.PublisherName == publisher.PublisherName && p.PublisherID != publisher.PublisherID))
			{
				ModelState.AddModelError("PublisherName", "此出版社已存在！");
				return View(publisher);
			}
			publisher.UpdatedAt = DateTime.Now;
			_context.Update(publisher);
			await _context.SaveChangesAsync();

			TempData["Success"] = "出版社修改成功！"; // 修改完成訊息

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 刪除(Delete)

		// GET: /Books/Publishers/Delete/5
		// 顯示刪除確認頁面
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FirstOrDefaultAsync(m => m.PublisherID == id);
			if (publisher == null) return NotFound();

			return View(publisher); // 傳模型到刪除確認頁
		}

		// POST: /Books/Publishers/Delete/5
		[HttpPost, ActionName("Delete")] // 與 GET 共用 URL
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var publisher = await _context.Publishers.FindAsync(id);
			if (publisher != null)
			{
				_context.Publishers.Remove(publisher);
				await _context.SaveChangesAsync();

				TempData["Success"] = "出版社刪除成功！"; // 刪除完成訊息
			}
			else
			{
				TempData["Error"] = "刪除失敗，出版社不存在！"; // 刪除失敗訊息
			}
			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 詳細資訊(Details)

		// GET: /Books/Publishers/Details/5
		// 顯示單一出版社的完整資訊
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FirstOrDefaultAsync(m => m.PublisherID == id);
			if (publisher == null) return NotFound();

			return View(publisher); // 把模型傳給 View
		}

		#endregion
	}
}
