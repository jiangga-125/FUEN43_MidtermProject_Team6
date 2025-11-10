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

		#region Index
		// GET: /Books/Inventory 可帶 query: ?q=關鍵字&bookId=123
		public async Task<IActionResult> Index(string? q, int? bookId)
		{
			// 1) 取得書籍清單（供上方下拉選擇）
			var books = await _db.Books
				.OrderBy(b => b.Title)
				.Select(b => new { b.BookID, b.Title })
				.ToListAsync();

			ViewBag.Books = books;
			ViewBag.q = q;

			// 2) 決定目前要顯示哪本書：若 URL 有 bookId 用它，否則預設選第一本（若有）
			if (!bookId.HasValue && books.Any())
			{
				bookId = books.First().BookID;
			}

			if (!bookId.HasValue)
			{
				// 沒有書籍資料
				ViewBag.BookID = 0;
				ViewBag.BookTitle = "";
				ViewBag.Rows = new List<object>();
				return View();
			}

			// 3) 取得 book 與各據點庫存（只取啟用據點）
			var book = await _db.Books.FirstOrDefaultAsync(b => b.BookID == bookId.Value);
			if (book == null)
			{
				TempData["err"] = "找不到指定的書目";
				ViewBag.BookID = 0;
				ViewBag.BookTitle = "";
				ViewBag.Rows = new List<object>();
				return View();
			}

			var rows = await _db.Branches
				.Where(b => b.IsActive)
				.OrderBy(b => b.BranchID)
				.Select(b => new
				{
					b.BranchID,
					b.BranchName,
					OnHand = _db.BookInventories
								.Where(i => i.BookID == bookId.Value && i.BranchID == b.BranchID)
								.Select(i => i.OnHand).FirstOrDefault(),
					Reserved = _db.BookInventories
								.Where(i => i.BookID == bookId.Value && i.BranchID == b.BranchID)
								.Select(i => i.Reserved).FirstOrDefault(),
				})
				.ToListAsync();

			ViewBag.BookID = bookId.Value;
			ViewBag.BookTitle = book.Title;
			ViewBag.Rows = rows;

			return View();
		}
		#endregion

		#region GetRows
		// GET: /Books/Inventory/GetRows?bookId=5
		[HttpGet]
		public async Task<IActionResult> GetRows(int bookId)
		{
			var rows = await _db.Branches
				.Where(b => b.IsActive)
				.OrderBy(b => b.BranchID)
				.Select(b => new
				{
					b.BranchID,
					b.BranchName,
					OnHand = _db.BookInventories
								.Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								.Select(i => i.OnHand).FirstOrDefault(),
					Reserved = _db.BookInventories
								.Where(i => i.BookID == bookId && i.BranchID == b.BranchID)
								.Select(i => i.Reserved).FirstOrDefault(),
				})
				.ToListAsync();

			return Json(rows);
		}
		#endregion

		#region Edit
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
		#endregion

		#region Save 
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
		#endregion

		#region Transfer
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
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "來源與目的據點不可相同。" });

				TempData["err"] = "來源與目的據點不可相同。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}
			if (quantity <= 0)
			{
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "調撥數量需大於 0。" });

				TempData["err"] = "調撥數量需大於 0。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}

			var book = await _db.Books.FirstOrDefaultAsync(b => b.BookID == bookId);
			if (book == null)
			{
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "找不到指定的書目。" });

				return NotFound();
			}

			var branches = await _db.Branches
				.Where(b => b.IsActive && (b.BranchID == fromBranchId || b.BranchID == toBranchId))
				.Select(b => b.BranchID)
				.ToListAsync();

			if (branches.Count != 2)
			{
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "來源或目的據點不存在，或該據點已停用。" });

				TempData["err"] = "來源或目的據點不存在，或該據點已停用。";
				return RedirectToAction(nameof(Transfer), new { bookId });
			}

			using var tx = await _db.Database.BeginTransactionAsync();
			try
			{
				var src = await _db.BookInventories
					.FirstOrDefaultAsync(x => x.BookID == bookId && x.BranchID == fromBranchId);

				var srcOnHand = src?.OnHand ?? 0;
				var srcReserved = src?.Reserved ?? 0;
				var srcAvailable = srcOnHand - srcReserved;
				if (srcAvailable < quantity)
				{
					if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
						return Json(new { ok = false, message = $"來源據點可用數量不足（可用 {srcAvailable} 本）。" });

					TempData["err"] = $"來源據點可用數量不足（可用 {srcAvailable} 本）。";
					return RedirectToAction(nameof(Transfer), new { bookId });
				}

				if (src == null)
				{
					if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
						return Json(new { ok = false, message = "來源據點庫存紀錄不存在，無法執行調撥。" });

					TempData["err"] = "來源據點庫存紀錄不存在，無法執行調撥。";
					return RedirectToAction(nameof(Transfer), new { bookId });
				}

				var dest = await _db.BookInventories
					.FirstOrDefaultAsync(x => x.BookID == bookId && x.BranchID == toBranchId);

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
					dest.OnHand = checked(dest.OnHand + quantity);
					dest.UpdatedAt = DateTime.UtcNow;
					_db.Entry(dest).State = EntityState.Modified;
				}

				await _db.SaveChangesAsync();
				await tx.CommitAsync();

				// 取得來源 / 目的 店名（供回傳顯示）
				var fromName = await _db.Branches.Where(b => b.BranchID == fromBranchId).Select(b => b.BranchName).FirstOrDefaultAsync();
				var toName = await _db.Branches.Where(b => b.BranchID == toBranchId).Select(b => b.BranchName).FirstOrDefaultAsync();

				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
				{
					return Json(new
					{
						ok = true,
						message = $"成功將 {quantity} 本從 {fromName} 調撥至 {toName}。",
						fromName,
						toName,
						quantity
					});
				}

				// 非 Ajax：維持舊行為但把 TempData 訊息改為顯示店名（較友善）
				TempData["ok"] = $"成功將 {quantity} 本從 {fromName} 調撥至 {toName}。";
			}
			catch (DbUpdateConcurrencyException)
			{
				await tx.RollbackAsync();
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "發生並發衝突，請重新整理後再試一次。" });

				TempData["err"] = "發生並發衝突，請重新整理後再試一次。";
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "調撥失敗: " + ex.Message });

				TempData["err"] = "調撥失敗: " + ex.Message;
			}

			return RedirectToAction(nameof(Edit), new { bookId });
		}
		#endregion
	}
}
