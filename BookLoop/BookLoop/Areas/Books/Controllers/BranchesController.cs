using BookLoop.Data;
using BookLoop.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class BranchesController : Controller
	{
		private readonly BookSystemContext _db;
		public BranchesController(BookSystemContext db) => _db = db;

		// GET: /Books/Branches?q=關鍵字&showInactive=true&returnUrl=/Borrow/Listings
		[HttpGet]
		public async Task<IActionResult> Index(string? q, bool showInactive = false, string? returnUrl = null)
		{
			// 返回上一頁的連結（書籍清單作為預設）
			ViewBag.ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
				? returnUrl
				: Url.Action("Index", "Books", new { area = "Books" });

			// 篩選
			var query = _db.Branches.AsQueryable();
			if (!showInactive) query = query.Where(b => b.IsActive);
			if (!string.IsNullOrWhiteSpace(q)) query = query.Where(b => b.BranchName.Contains(q));

			var list = await query
				.OrderByDescending(b => b.IsActive)
				.ThenBy(b => b.BranchID)
				.ToListAsync();

			ViewBag.q = q;
			ViewBag.showInactive = showInactive;
			return View(list);
		}


		// GET: /Books/Branches/Create
		public IActionResult Create() => View(new Branch { IsActive = true });

		// POST: /Books/Branches/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Branch input)
		{
			if (string.IsNullOrWhiteSpace(input.BranchName))
				ModelState.AddModelError(nameof(input.BranchName), "據點名稱必填");

			var exists = await _db.Branches
				.AnyAsync(b => b.BranchName == input.BranchName);
			if (exists)
				ModelState.AddModelError(nameof(input.BranchName), "據點名稱已存在");

			if (!ModelState.IsValid) return View(input);

			input.CreatedAt = DateTime.UtcNow;
			input.UpdatedAt = DateTime.UtcNow;
			_db.Branches.Add(input);
			await _db.SaveChangesAsync();
			TempData["ok"] = "據點已建立";
			return RedirectToAction(nameof(Index));
		}

		// GET: /Books/Branches/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			var br = await _db.Branches.FindAsync(id);
			if (br == null) return NotFound();
			return View(br);
		}

		// POST: /Books/Branches/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Branch input)
		{
			if (id != input.BranchID) return NotFound();

			if (string.IsNullOrWhiteSpace(input.BranchName))
				ModelState.AddModelError(nameof(input.BranchName), "據點名稱必填");

			var nameUsed = await _db.Branches
				.AnyAsync(b => b.BranchName == input.BranchName && b.BranchID != id);
			if (nameUsed)
				ModelState.AddModelError(nameof(input.BranchName), "據點名稱已存在");

			if (!ModelState.IsValid) return View(input);

			var br = await _db.Branches.FindAsync(id);
			if (br == null) return NotFound();

			br.BranchName = input.BranchName;
			br.IsActive = input.IsActive;
			br.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();
			TempData["ok"] = "據點已更新";
			return RedirectToAction(nameof(Index));
		}

		// POST: /Books/Branches/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id)
		{
			var br = await _db.Branches.FindAsync(id);
			if (br == null) return NotFound();

			// 如有庫存紀錄則不允許硬刪，改提示用停用
			bool hasInventory = await _db.BookInventories.AnyAsync(i => i.BranchID == id);
			if (hasInventory)
			{
				TempData["err"] = "此據點已有庫存紀錄，無法刪除。請改用「停用」。";
				return RedirectToAction(nameof(Index));
			}

			_db.Branches.Remove(br);
			await _db.SaveChangesAsync();
			TempData["ok"] = "據點已刪除";
			return RedirectToAction(nameof(Index));
		}

		// POST: /Books/Branches/Toggle/5  （一鍵啟用/停用）
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Toggle(int id)
		{
			var br = await _db.Branches.FindAsync(id);
			if (br == null) return NotFound();
			br.IsActive = !br.IsActive;
			br.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();
			TempData["ok"] = br.IsActive ? "據點已啟用" : "據點已停用";
			return RedirectToAction(nameof(Index));
		}
	}
}
