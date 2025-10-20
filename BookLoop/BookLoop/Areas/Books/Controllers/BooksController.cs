using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using BookLoop.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class BooksController : Controller
	{
		private readonly BookSystemContext _context;
		private readonly BookService _bookService;
		private readonly IImageValidator _imageValidator;

		public BooksController(BookSystemContext context, BookService bookService, IImageValidator imageValidator)
		{
			_context = context;
			_bookService = bookService;
			_imageValidator = imageValidator;
		}

		#region 列表(Index)

		// GET: /Books/Books
		public async Task<IActionResult> Index()
		{
			// 你原本的列表（用 Service 也可以）
			var list = await _bookService.GetBooksAsync();

			// 取每本書的主圖（IsPrimary=1），做成 BookID -> FilePath 的 map
			var primaryMap = await _context.BookImages
				.Where(i => i.IsPrimary)
				.GroupBy(i => i.BookID)
				.Select(g => new
				{
					BookID = g.Key,
					FilePath = g
						.OrderByDescending(x => x.ImageID)
						.Select(x => x.FilePath)
						.FirstOrDefault()
				})
				.ToDictionaryAsync(x => x.BookID, x => x.FilePath ?? "");

			ViewBag.PrimaryImages = primaryMap;

			var totals = await _context.BookInventories
			.GroupBy(i => i.BookID)
			.Select(g => new { BookID = g.Key, Total = g.Sum(x => x.OnHand - x.Reserved) })
			.ToDictionaryAsync(x => x.BookID, x => x.Total);
			ViewBag.InventoryTotals = totals;

			return View(list);
		}

		// ⬇️新增 Action：提供各據點可售量，給前台彈窗用
		// GET: /Books/Books/Availability/5
		[HttpGet]
		public async Task<IActionResult> Availability(int id)
		{
			var items = await _context.BookInventories
				.Where(i => i.BookID == id)
				.Select(i => new
				{
					branchName = i.Branch.BranchName,
					available = i.OnHand - i.Reserved
				})
				.OrderByDescending(x => x.available)
				.ToListAsync();

			return Json(items);
		}

		#endregion

		#region 新增(Create)

		// GET: /Books/Books/Create
		public async Task<IActionResult> Create()
		{
			await LoadDropdownsAsync();
			return View();
		}

		// POST: /Books/Books/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Book book, IFormFile? coverFile, string? coverUrl)
		{
			book.Slug = SlugHelper.Generate(book.Title);
			ModelState.Remove("Slug"); // 保險

			if (!ModelState.IsValid)
			{
				await LoadDropdownsAsync();
				return View(book);
			}

			if (_context.Books.Any(b => b.ISBN == book.ISBN))
			{
				ModelState.AddModelError("Isbn", "這個 ISBN 已經存在，請輸入新的。");
				return View(book);
			}

			// 使用 Service 新增 Book（會處理 Slug / CreatedAt / UpdatedAt 等）
			await _bookService.AddBookAsync(book);

			string? filePathToSave = null;

			// 1) 如果上傳檔案 -> 用 image validator 驗證
			if (coverFile != null && coverFile.Length > 0)
			{
				var validation = await _imageValidator.ValidateFileAsync(coverFile);
				if (!validation.IsValid)
				{
					ModelState.AddModelError("coverFile", validation.Error ?? "上傳圖片驗證失敗");
					await LoadDropdownsAsync();
					return View(book);
				}

				// 驗證通過 -> 存檔
				var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "books");
				Directory.CreateDirectory(uploadsDir);

				var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				var savePath = Path.Combine(uploadsDir, fileName);
				using (var fs = new FileStream(savePath, FileMode.Create))
				{
					await coverFile.CopyToAsync(fs);
				}
				filePathToSave = fileName; // 存檔名（view 判斷非 http 的情況）
			}
			// 2) 否則若有填 URL -> 用 validator 驗證 URL
			else if (!string.IsNullOrWhiteSpace(coverUrl))
			{
				var validation = await _imageValidator.ValidateUrlAsync(coverUrl);
				if (!validation.IsValid)
				{
					ModelState.AddModelError("coverUrl", validation.Error ?? "圖片 URL 驗證失敗");
					await LoadDropdownsAsync();
					return View(book);
				}
				filePathToSave = coverUrl;
			}

			// 3) 如果有驗證通過的圖片來源，新增 BookImage（設為主圖）
			if (!string.IsNullOrWhiteSpace(filePathToSave))
			{
				// 取消既有的主圖（保險）
				var existingPrimaries = await _context.BookImages.Where(bi => bi.BookID == book.BookID && bi.IsPrimary).ToListAsync();
				foreach (var p in existingPrimaries)
				{
					p.IsPrimary = false;
					p.UpdatedAt = DateTime.UtcNow;
				}

				var newImg = new BookImage
				{
					BookID = book.BookID,
					FilePath = filePathToSave,
					Caption = null,
					IsPrimary = true,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};

				_context.BookImages.Add(newImg);
				await _context.SaveChangesAsync();
			}

			TempData["Success"] = "書籍新增成功！";
			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 修改(Edit)

		// GET: /Books/Books/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var book = await _context.Books
				.Include(b => b.BookImages)
				.Include(b => b.Publisher)
				.Include(b => b.Category)
				.FirstOrDefaultAsync(b => b.BookID == id.Value);

			if (book == null) return NotFound();

			await LoadDropdownsAsync();
			return View(book);
		}

		// POST: /Books/Books/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Book book, IFormFile? coverFile, string? coverUrl)
		{
			if (id != book.BookID) return NotFound();

			book.Slug = SlugHelper.Generate(book.Title);
			ModelState.Remove("Slug"); // 保險

			if (!ModelState.IsValid)
			{
				await LoadDropdownsAsync();
				return View(book);
			}
			var existing = await _context.Books
			.Include(b => b.BookImages)
			.FirstOrDefaultAsync(b => b.BookID == id);
			if (existing == null) return NotFound();

			// 只更新有改的欄位
			existing.Title = book.Title;
			existing.ISBN = book.ISBN;
			existing.PublisherID = book.PublisherID;
			existing.CategoryID = book.CategoryID;
			existing.Slug = SlugHelper.Generate(book.Title);
			existing.UpdatedAt = DateTime.UtcNow;

			if (_context.Books.Any(b => b.ISBN == book.ISBN && b.BookID != book.BookID))
			{
				ModelState.AddModelError("Isbn", "這個 ISBN 已經存在，請輸入新的。");
				return View(book);
			}

			// 圖片處理（跟 Create 類似）
			string? filePathToSave = null;

			if (coverFile != null && coverFile.Length > 0)
			{
				// **使用 validation 物件，不做解構**
				var validation = await _imageValidator.ValidateFileAsync(coverFile);
				if (!validation.IsValid)
				{
					ModelState.AddModelError("coverFile", validation.Error ?? "上傳圖片驗證失敗");
					await LoadDropdownsAsync();
					return View(book);
				}

				// 驗證通過 -> 存檔
				var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "books");
				Directory.CreateDirectory(uploadsDir);

				var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				var savePath = Path.Combine(uploadsDir, fileName);
				using (var fs = new FileStream(savePath, FileMode.Create))
				{
					await coverFile.CopyToAsync(fs);
				}
				filePathToSave = fileName; // 存檔名（view 判斷非 http 的情況）
			}
			else if (!string.IsNullOrWhiteSpace(coverUrl))
			{
				var validation = await _imageValidator.ValidateUrlAsync(coverUrl);
				if (!validation.IsValid)
				{
					ModelState.AddModelError("coverUrl", validation.Error ?? "圖片 URL 驗證失敗");
					await LoadDropdownsAsync();
					return View(book);
				}
				filePathToSave = coverUrl;
			}

			if (!string.IsNullOrWhiteSpace(filePathToSave))
			{
				// 取消既有主圖 & 新增 BookImage（同 Create）
				var existingPrimaries = await _context.BookImages
					.Where(bi => bi.BookID == book.BookID && bi.IsPrimary)
					.ToListAsync();

				foreach (var p in existingPrimaries)
				{
					p.IsPrimary = false;
					p.UpdatedAt = DateTime.UtcNow;
				}

				var newImg = new BookImage
				{
					BookID = book.BookID,
					FilePath = filePathToSave,
					Caption = null,
					IsPrimary = true,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};

				_context.BookImages.Add(newImg);
				await _context.SaveChangesAsync();
			}

			TempData["Success"] = "書籍修改成功！";
			return RedirectToAction(nameof(Index));
		}


		#endregion

		#region 刪除(Delete)

		// GET: /Books/Books/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var book = await _bookService.GetBookByIdAsync(id.Value);
			if (book == null) return NotFound();

			return View(book);
		}

		// POST: /Books/Books/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var book = await _context.Books.FindAsync(id);
			if (book != null)
			{
				_context.Books.Remove(book);
				await _context.SaveChangesAsync();
				TempData["Success"] = "書籍刪除成功！";
			}
			else
			{
				TempData["Error"] = "刪除失敗，書籍不存在！";
			}

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region 詳細資訊(Details)

		// GET: /Books/Books/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var book = await _bookService.GetBookWithRelationsAsync(id.Value);
			if (book == null) return NotFound();

			var stocks = await _context.BookInventories
				.Where(x => x.BookID == id.Value)
				.Select(x => new
				{
					x.BranchID,
					x.Branch.BranchName,
					Available = x.OnHand - x.Reserved
				})
				.Where(x => x.Available > 0)
				.OrderByDescending(x => x.Available)
				.ToListAsync();

			ViewBag.BranchStocks = stocks;
			ViewBag.TotalAvailable = stocks.Sum(s => s.Available);

			return View(book);
		}

		#endregion

		#region 搜尋 (Search)
		[HttpGet]
		public async Task<IActionResult> Search(string search)
		{
			var query = _context.Books
				.Include(b => b.Publisher)
				.Include(b => b.Category)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				query = query.Where(b => b.Title.Contains(search) || b.ISBN.Contains(search));
			}

			var books = await query.ToListAsync();

			// 帶回封面
			ViewBag.PrimaryImages = await _context.BookImages
				.Where(i => i.IsPrimary)
				.GroupBy(i => i.BookID)
				.ToDictionaryAsync(g => g.Key, g => g.First().FilePath);

			// 新增：庫存 map
			ViewBag.StockMap = await _context.BookInventories
				.GroupBy(bi => bi.BookID)
				.Select(g => new
				{
					BookID = g.Key,
					TotalAvailable = g.Sum(x => x.OnHand - x.Reserved)
				})
				.ToDictionaryAsync(x => x.BookID, x => x.TotalAvailable);

			return PartialView("_BooksTable", books);
		}
		#endregion

		#region 下拉選單

		private async Task LoadDropdownsAsync()
		{
			ViewBag.PublisherList = await _context.Publishers
				.OrderBy(p => p.PublisherName)
				.Select(p => new SelectListItem
				{
					Value = p.PublisherID.ToString(),
					Text = p.PublisherName
				})
				.ToListAsync();

			ViewBag.CategoryList = await _context.Categories
				.OrderBy(c => c.CategoryName)
				.Select(c => new SelectListItem
				{
					Value = c.CategoryID.ToString(),
					Text = c.CategoryName
				})
				.ToListAsync();
		}

		#endregion

		// 測試用：顯示 Books 與 BookImages
		public async Task<IActionResult> TestImages()
		{
			var books = await _context.Books
				.Include(b => b.BookImages)
				.Take(5)
				.ToListAsync();

			return View(books);
		}
		
	}
}
