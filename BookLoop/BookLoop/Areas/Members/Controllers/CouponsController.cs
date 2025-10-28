using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using BookLoop.Data;                    // MemberContext
using BookLoop.Models;                  // Coupon / Category / CouponCategory
using BookLoop.Areas.Members.ViewModels; // CouponEditVm

namespace BookLoop.Areas.Members.Controllers
{
	[Area("Members")]
	[Route("Members/[controller]/[action]")]
	public class CouponsController : Controller
	{
		private readonly MemberContext _db;
		public CouponsController(MemberContext db) => _db = db;

		// ========== 共用：載入分類清單 ==========
		private async Task FillCategoryOptionsAsync(CouponEditVm vm)
		{
			vm.CategoryOptions = await _db.Categories
				.OrderBy(c => c.CategoryName)
				.Select(c => new SelectListItem
				{
					Value = (c.CategoryID).ToString(),
					Text = (c.CategoryName),
					Selected = vm.SelectedCategoryIds.Contains(c.CategoryID)
				})
				.ToListAsync();
		}

		// ========== 列表 ==========
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var list = await _db.Coupons
				.Include(c => c.CouponCategories)
					.ThenInclude(cc => cc.Category)   // 讓 View 能用 cc.Category?.Name
				.OrderByDescending(c => c.CouponId)
				.ToListAsync();

			return View(list);
		}


		// ========== Create ==========
		[HttpGet]
		public async Task<IActionResult> Create()
		{
			var vm = new CouponEditVm();
			await FillCategoryOptionsAsync(vm);
			return View(vm); // 視圖的 @model 請改用 CouponEditVm
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CouponEditVm vm)
		{
			if (!ModelState.IsValid)
			{
				await FillCategoryOptionsAsync(vm);
				return View(vm);
			}

			using var tx = await _db.Database.BeginTransactionAsync();
			try
			{
				var coupon = new Coupon
				{
					Name = vm.Name,
					Code = vm.Code,
					DiscountType = vm.DiscountType,
					DiscountValue = vm.DiscountValue,
					MinOrderAmount = vm.MinOrderAmount,
					MaxDiscountAmount = vm.MaxDiscountAmount,
					StartAt = vm.StartAt,
					EndAt = vm.EndAt,
					IsActive = vm.IsActive,
					MaxUsesPerMember = vm.MaxUsesPerMember,
					Description = vm.Description,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};

				_db.Coupons.Add(coupon);
				await _db.SaveChangesAsync(); // 取得 CouponId

				if (vm.SelectedCategoryIds?.Count > 0)
				{
					foreach (var catId in vm.SelectedCategoryIds.Distinct())
					{
						_db.CouponCategories.Add(new CouponCategory
						{
							CouponID = coupon.CouponId,
							CategoryID = catId
						});
					}
					await _db.SaveChangesAsync();
				}

				// TermsText：如果要入庫到別表，這裡處理（本例先略過）
				await tx.CommitAsync();

				TempData["Msg"] = "新增成功";
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				ModelState.AddModelError("", ex.Message);
				await FillCategoryOptionsAsync(vm);
				return View(vm);
			}
		}

		// ========== Edit ==========
		[HttpGet("{id:int}")]
		public async Task<IActionResult> Edit(int id)
		{
			var c = await _db.Coupons
				.Include(x => x.CouponCategories)
				.FirstOrDefaultAsync(x => x.CouponId == id);
			if (c == null) return NotFound();

			var vm = new CouponEditVm
			{
				CouponID = c.CouponId,
				Name = c.Name,
				Code = c.Code,
				DiscountType = c.DiscountType,
				DiscountValue = c.DiscountValue,
				MinOrderAmount = c.MinOrderAmount,
				MaxDiscountAmount = c.MaxDiscountAmount,
				StartAt = c.StartAt,
				EndAt = c.EndAt,
				IsActive = c.IsActive,
				MaxUsesPerMember = c.MaxUsesPerMember,
				Description = c.Description,
				SelectedCategoryIds = c.CouponCategories.Select(cc => cc.CategoryID).ToList()
			};
			await FillCategoryOptionsAsync(vm);
			return View(vm); // 視圖使用 CouponEditVm
		}

		[HttpPost("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, CouponEditVm vm)
		{
			if (!ModelState.IsValid)
			{
				await FillCategoryOptionsAsync(vm);
				return View(vm);
			}

			using var tx = await _db.Database.BeginTransactionAsync();
			try
			{
				var c = await _db.Coupons
					.Include(x => x.CouponCategories)
					.FirstOrDefaultAsync(x => x.CouponId == id);
				if (c == null) return NotFound();

				// VM -> Entity
				c.Name = vm.Name;
				c.Code = vm.Code;
				c.DiscountType = vm.DiscountType;
				c.DiscountValue = vm.DiscountValue;
				c.MinOrderAmount = vm.MinOrderAmount;
				c.MaxDiscountAmount = vm.MaxDiscountAmount;
				c.StartAt = vm.StartAt;
				c.EndAt = vm.EndAt;
				c.IsActive = vm.IsActive;
				c.MaxUsesPerMember = vm.MaxUsesPerMember;
				c.Description = vm.Description;
				c.UpdatedAt = DateTime.UtcNow;

				await _db.SaveChangesAsync();

				// 重寫橋接表
				if (c.CouponCategories.Any())
				{
					_db.CouponCategories.RemoveRange(c.CouponCategories);
					await _db.SaveChangesAsync();
				}
				if (vm.SelectedCategoryIds?.Count > 0)
				{
					foreach (var catId in vm.SelectedCategoryIds.Distinct())
					{
						_db.CouponCategories.Add(new CouponCategory
						{
							CouponID = c.CouponId,
							CategoryID = catId
						});
					}
					await _db.SaveChangesAsync();
				}

				// TermsText：如需保存，這裡處理
				await tx.CommitAsync();

				TempData["Msg"] = "修改優惠券成功！";
				return RedirectToAction(nameof(Index));
			}
			catch (DbUpdateConcurrencyException)
			{
				await tx.RollbackAsync();
				ModelState.AddModelError("", "資料已被他人修改，請重整後再試。");
				await FillCategoryOptionsAsync(vm);
				return View(vm);
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				ModelState.AddModelError("", ex.Message);
				await FillCategoryOptionsAsync(vm);
				return View(vm);
			}
		}

		// ========== 啟停 ==========
		[HttpPost("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var coupon = await _db.Coupons.FindAsync(id);
			if (coupon == null) return NotFound();

			coupon.IsActive = !coupon.IsActive;
			coupon.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			TempData["Msg"] = coupon.IsActive ? "優惠券已啟用" : "優惠券已停用";
			return RedirectToAction(nameof(Index));
		}

		// ========== Debug：看橋接資料 ==========
		[HttpGet("/Members/Coupons/Debug/CouponCategories")]
		public async Task<IActionResult> DebugCouponCategories()
		{
			var data = await _db.CouponCategories
	.Include(cc => cc.Coupon)
	.Include(cc => cc.Category)
	.OrderBy(cc => cc.Category!.CategoryName)          // ← 排序放這裡（集合）
	.Take(20)
	.Select(cc => new {
		cc.CouponCategoryID,
		cc.CouponID,
		CouponName = cc.Coupon!.Name,
		cc.CategoryID,
		CategoryName = cc.Category!.CategoryName        // ← 這裡只是字串，不能再 .OrderBy(...)
	})
	.ToListAsync();


			return Json(new { count = data.Count, items = data });
		}
	}
}
