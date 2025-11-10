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
		// GET: /Books/Inventory  或 /Books/Inventory/Index
		public async Task<IActionResult> Index(string? q)
		{
			// 簡單搜尋（可用書名模糊搜尋）
			var query = _db.Books.AsQueryable();
			if (!string.IsNullOrWhiteSpace(q))
				query = query.Where(b => b.Title.Contains(q));

			var list = await query
				.OrderBy(b => b.BookID)
				.Take(200) // 預防一次拉太多筆，必要時改成分頁
				.ToListAsync();

			ViewBag.q = q;
			return View(list); // 會對應到 Areas/Books/Views/Inventory/Index.cshtml
		}

		// GET: /Books/Inventory/Edit/5 或 /Books/Inventory/Edit?id=5 或 /Books/Inventory/Edit?bookId=5
		public async Task<IActionResult> Edit(int? id)
		{
			if (!id.HasValue)
			{
				TempData["err"] = "請先從書籍清單選擇要管理庫存的書目。";
				return RedirectToAction(nameof(Index));
			}
			int bookId = id.Value;

			var book = await _db.Books.FirstOrDefaultAsync(x => x.BookID == bookId);
			if (book == null) return NotFound();

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
			ViewBag.Rows = rows;
			return View("Edit");
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

		// GET: /Books/Inventory/Transfer?bookId=5
		public async Task<IActionResult> Transfer(int bookId)
		{
			var book = await _db.Books.FirstOrDefaultAsync(x => x.BookID == bookId);
			if (book == null) return NotFound();

			// 只列出啟用的據點供選擇（前端 select）
			var rows = await _db.Branches
				.Where(b => b.IsActive)
				.OrderBy(b => b.BranchID)
				.Select(b => new
				{
					b.BranchID,
					b.BranchName,
					OnHand = _db.BookInventories
								.Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								.Select(i => i.OnHand)
								.FirstOrDefault()
				})
				.ToListAsync();

			ViewBag.BookID = bookId;
			ViewBag.BookTitle = book.Title;
			ViewBag.Rows = rows;
			return View(); // Views/Books/Inventory/Transfer.cshtml
		}

		// POST: /Books/Inventory/Transfer
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Transfer(int bookId, int fromBranchId, int toBranchId, int quantity, string reason = "")
		{
			if (fromBranchId == toBranchId)
			{
				TempData["err"] = "來源與目的據點不可相同。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}
			if (quantity <= 0)
			{
				TempData["err"] = "調撥數量需大於 0。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}

			// 確認 book 存在
			var book = await _db.Books.FirstOrDefaultAsync(b => b.BookID == bookId);
			if (book == null) return NotFound();

			// **伺服器端驗證：來源與目的據點都必須是資料庫中存在且為啟用（IsActive = true）**
			var branches = await _db.Branches
				.Where(b => b.IsActive && (b.BranchID == fromBranchId || b.BranchID == toBranchId))
				.Select(b => b.BranchID)
				.ToListAsync();

			if (branches.Count != 2)
			{
				TempData["err"] = "來源或目的據點不存在，或該據點已停用。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}

			using var tx = await _db.Database.BeginTransactionAsync();
			try
			{
				// 讀來源庫存（若無資料，視為 OnHand = 0）
				var src = await _db.BookInventories
					.FirstOrDefaultAsync(x => x.BookID == bookId && x.BranchID == fromBranchId);

				var srcOnHand = src?.OnHand ?? 0;
				var srcReserved = src?.Reserved ?? 0;
				var srcAvailable = srcOnHand - srcReserved;
				if (srcAvailable < quantity)
				{
					TempData["err"] = $"來源據點可用數量不足（可用 {srcAvailable} 本）。";
					return RedirectToAction(nameof(Transfer), new { bookId });
				}

				// 讀或建立目的地 inventory row
				var dest = await _db.BookInventories
					.FirstOrDefaultAsync(x => x.BookID == bookId && x.BranchID == toBranchId);

				// 來源理論上應該存在（但若不存在也已視為 OnHand=0），以下按邏輯處理
				if (src == null)
				{
					// 保護性檢查：來源沒有紀錄，但因為可用數量檢查會擋住，通常不會到這裡
					TempData["err"] = "來源據點庫存紀錄不存在，無法執行調撥。";
					return RedirectToAction(nameof(Transfer), new { bookId });
				}

				// 扣來源
				src.OnHand = Math.Max(0, src.OnHand - quantity);
				src.UpdatedAt = DateTime.UtcNow;
				_db.Entry(src).State = EntityState.Modified;

				// 增目的地（若不存在則新增）
				if (dest == null)
				{
					dest = new BookInventory
					{
						BookID = bookId,
						BranchID = toBranchId,
						OnHand = quantity,
						Reserved = 0,
						UpdatedAt = DateTime.UtcNow
					};
					_db.BookInventories.Add(dest);
				}
				else
				{
					dest.OnHand = checked(dest.OnHand + quantity); // 使用 checked 防 overflow（可選）
					dest.UpdatedAt = DateTime.UtcNow;
					_db.Entry(dest).State = EntityState.Modified;
				}

				await _db.SaveChangesAsync();
				await tx.CommitAsync();

				TempData["ok"] = $"成功將 {quantity} 本從據點 {fromBranchId} 調撥至據點 {toBranchId}。";
			}
			catch (DbUpdateConcurrencyException)
			{
				await tx.RollbackAsync();
				TempData["err"] = "發生並發衝突，請重新整理後再試一次。";
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				TempData["err"] = "調撥失敗: " + ex.Message;
			}

			return RedirectToAction(nameof(Edit), new { bookId });
		}
	}
}
