using BookLoop.Data;
using BookLoop.Models; // 換成你的命名空間
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers
{
	[Area("Members")]
	public class CouponsController : Controller
	{
		private readonly MemberContext _db;

		public CouponsController(MemberContext db)
		{
			_db = db;
		}

		// GET: Members/Coupon/Create
		public IActionResult Create()
		{
			return View();
		}

		// POST: Admin/Coupon/Create
		[HttpPost]
		public async Task<IActionResult> Create(Coupon coupon)
		{

			ModelState.Remove("RowVer");
			// DEBUG
			if (!ModelState.IsValid)
			{
				foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
				{
					Console.WriteLine("[MODEL ERROR] " + error.ErrorMessage);
				}
			}

			if (ModelState.IsValid)
			{
				coupon.CreatedAt = DateTime.Now;
				coupon.UpdatedAt = DateTime.Now;
				_db.Coupons.Add(coupon);
				await _db.SaveChangesAsync();
				TempData["Msg"] = "新增成功";
				return RedirectToAction("Index");
			}

			System.Diagnostics.Debug.WriteLine("[DEBUG] ModelState invalid");
			foreach (var e in ModelState.Values.SelectMany(v => v.Errors))
			{
				System.Diagnostics.Debug.WriteLine("[ERROR] " + e.ErrorMessage);
			}
			return View(coupon);
		}


		// GET: Members/Coupon
		public async Task<IActionResult> Index()
		{
			var list = await _db.Coupons.ToListAsync();
			return View(list);
		}

		// GET: /Coupon/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			var coupon = await _db.Coupons.FindAsync(id);
			if (coupon == null) return NotFound();
			return View(coupon);
		}

		// POST: /Coupon/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Coupon coupon)
		{

			if (id != coupon.CouponId) return NotFound();

			if (ModelState.IsValid)
			{
				_db.Update(coupon);
				await _db.SaveChangesAsync();
				TempData["Msg"] = "修改優惠券成功！";
				return RedirectToAction("Index");
			}
			return View(coupon);
		}

		// POST: /Coupon/ToggleActive/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var coupon = await _db.Coupons.FindAsync(id);
			if (coupon == null) return NotFound();

			// 反轉 IsActive 狀態
			coupon.IsActive = !coupon.IsActive;
			await _db.SaveChangesAsync();

			TempData["Msg"] = coupon.IsActive ? "優惠券已啟用" : "優惠券已停用";

			return RedirectToAction(nameof(Index));
		}

	}
}
