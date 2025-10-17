using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace BorrowSystem.Controllers
{
	[Area("Borrow")]
	public class UsedInventoryController : Controller
	{
		private readonly BorrowSystemContext _db;
		public UsedInventoryController(BorrowSystemContext db) => _db = db;

		// GET: /Borrow/UsedInventory/Edit/5
		public async Task<IActionResult> Edit(int listingID)
		{
			var listing = await _db.Listings.FindAsync(listingID);
			if (listing == null) return NotFound();

			var rows = await _db.Branches
				.OrderBy(b => b.BranchID)
				.Select(b => new
				{
					b.BranchID,
					b.BranchName,
					OnHand = _db.UsedBookInventories
								.Where(i => i.ListingID == listingID && i.BranchID == b.BranchID)
								.Select(i => i.OnHand)
								.FirstOrDefault(),
					Reserved = _db.UsedBookInventories
								.Where(i => i.ListingID == listingID && i.BranchID == b.BranchID)
								.Select(i => i.Reserved)
								.FirstOrDefault()
				}).ToListAsync();

			ViewBag.Listing = listing;
			ViewBag.Rows = rows;
			return View();
		}

		// POST: /Borrow/UsedInventory/Save
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Save(int listingID, int[] BranchID, int[] OnHand)
		{
			for (int i = 0; i < BranchID.Length; i++)
			{
				var bid = BranchID[i];
				var qty = Math.Max(0, OnHand[i]);
				var inv = await _db.UsedBookInventories
					.FirstOrDefaultAsync(x => x.ListingID == listingID && x.BranchID == bid);

				if (inv == null)
				{
					_db.UsedBookInventories.Add(new UsedBookInventory
					{
						ListingID = listingID,
						BranchID = bid,
						OnHand = qty,
						Reserved = 0,
						UpdatedAt = DateTime.UtcNow
					});
				}
				else
				{
					inv.OnHand = qty;
					inv.UpdatedAt = DateTime.UtcNow;
				}
			}

			await _db.SaveChangesAsync();
			TempData["ok"] = "庫存已更新";
			return RedirectToAction(nameof(Edit), new { listingID });
		}
	}
}
