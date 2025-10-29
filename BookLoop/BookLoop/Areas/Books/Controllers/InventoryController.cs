using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class InventoryController : Controller
	{
		private readonly BookSystemContext _db;
		public InventoryController(BookSystemContext db) => _db = db;

		// GET: /Books/Inventory/Edit?bookId=5  或  /Books/Inventory/Edit/5（若你的路由支援）
		public async Task<IActionResult> Edit(int bookId)
		{
			var book = await _db.Books.FirstOrDefaultAsync(x => x.BookID == bookId);
			if (book == null) return NotFound();

			// 取所有啟用據點 + 左連接庫存
			var rows = await _db.Branches
				.Where(b => b.IsActive)
				.OrderBy(b => b.BranchID)
				.Select(b => new
				{
					BranchID = b.BranchID,
					BranchName = b.BranchName,
					OnHand = _db.BookInventories
								.Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								.Select(i => i.OnHand).FirstOrDefault(),
					Reserved = _db.BookInventories
								  .Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								  .Select(i => i.Reserved).FirstOrDefault(),
					RowVersionBase64 = _db.BookInventories
								  .Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								  .Select(i => i.RowVersion).FirstOrDefault() != null
						? Convert.ToBase64String(_db.BookInventories
								  .Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								  .Select(i => i.RowVersion).FirstOrDefault()!)
						: ""
				})
				.ToListAsync();

			ViewBag.BookID = bookId;
			ViewBag.BookTitle = book.Title;
			ViewBag.Rows = rows; // 匿名型別清單，View 以 ViewBag 讀取
			return View();
		}

		// POST: /Books/Inventory/Save
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Save(int bookId, int[] BranchID, int[] OnHand, string[] RowVersionBase64)
		{
			// 逐列 upsert
			for (int i = 0; i < BranchID.Length; i++)
			{
				var bid = BranchID[i];
				var qoh = Math.Max(0, (i < OnHand.Length ? OnHand[i] : 0));
				var rvBase64 = (i < RowVersionBase64.Length ? RowVersionBase64[i] : null);

				var inv = await _db.BookInventories
					.FirstOrDefaultAsync(x => x.BookID == bookId && x.BranchID == bid);

				if (inv == null)
				{
					_db.BookInventories.Add(new BookInventory
					{
						BookID = bookId,
						BranchID = bid,
						OnHand = qoh,
						Reserved = 0,
						UpdatedAt = DateTime.UtcNow
					});
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(rvBase64))
					{
						try
						{
							var rv = Convert.FromBase64String(rvBase64);
							_db.Entry(inv).Property(p => p.RowVersion).OriginalValue = rv;
						}
						catch { /* 忽略 RowVersion 解析失敗 */ }
					}
					inv.OnHand = qoh;
					inv.UpdatedAt = DateTime.UtcNow;
				}
			}

			try
			{
				await _db.SaveChangesAsync();
				TempData["ok"] = "庫存已更新";
			}
			catch (DbUpdateConcurrencyException)
			{
				TempData["err"] = "有別人同時修改了庫存，請重新整理後再試一次。";
			}

			return RedirectToAction(nameof(Edit), new { bookId });
		}
	}
}
