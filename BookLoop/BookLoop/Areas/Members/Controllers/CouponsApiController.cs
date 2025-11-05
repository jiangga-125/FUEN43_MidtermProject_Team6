using BookLoop.Services;
using BookLoop.Services.Coupons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookLoop.Data;
using System.Linq;

namespace BookLoop.Areas.Store.Controllers
{
	[AllowAnonymous]
	[Area("Store")]
	[Route("api/[area]/[controller]/[action]")]
	[ApiController]
	public class CouponsApiController : ControllerBase
	{
		private readonly CouponService _couponService;
		private readonly MemberContext _context;

		public CouponsApiController(CouponService couponService, MemberContext memberContext)
		{
			_couponService = couponService;
			_context = memberContext;
		}

		// ✅ 取得使用者可用優惠券列表
		[HttpGet]
		public IActionResult List(int memberId = 1)
		{
			var list = _couponService.GetAvailableCoupons(memberId);
			return Ok(list);
		}

		// ✅ 套用優惠券（預覽折扣金額）
		[HttpPost]
		public async Task<IActionResult> Apply([FromBody] ApplyCouponRequest req)
		{
			var result = await _couponService.PreviewDiscountAsync(req.Code, req.MemberID, req.Subtotal);

			if (!result.IsSuccess)
				return BadRequest(new { message = result.ErrorMessage });

			return Ok(new
			{
				discount = result.Value.Discount,
				rule = result.Value.RuleText
			});
		}

		// ✅ 根據代碼查優惠券（提供前端 Create 頁面使用）
		[HttpGet]
		public IActionResult GetByCode(string code, decimal? amount = null)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(code))
					return new JsonResult(new { success = false, message = "請輸入代碼" });

				var coupon = _context.Coupons
					.Where(c => c.Code == code)
					.Select(c => new
					{
						c.CouponId,
						c.Name,
						c.DiscountType,
						c.DiscountValue,
						c.MinOrderAmount,
						c.StartAt,
						c.EndAt,
						c.IsActive
					})
					.FirstOrDefault();

				if (coupon == null)
					return new JsonResult(new { success = false, message = "找不到此優惠券代碼" });

				// 🟡 檢查啟用狀態
				if (coupon.IsActive == false)
					return new JsonResult(new { success = false, message = "此優惠券尚未啟用" });

				// 🟡 檢查時間
				var now = DateTime.UtcNow;
				if (coupon.StartAt.HasValue && now < coupon.StartAt.Value)
					return new JsonResult(new { success = false, message = "此優惠券尚未開始" });
				if (coupon.EndAt.HasValue && now > coupon.EndAt.Value)
					return new JsonResult(new { success = false, message = "此優惠券已過期" });

				// 🟡 檢查最低金額（如果有）
				if (coupon.MinOrderAmount.HasValue && amount.HasValue && amount.Value < coupon.MinOrderAmount.Value)
					return new JsonResult(new
					{
						success = false,
						message = $"未達最低消費金額 {coupon.MinOrderAmount.Value:N0} 元"
					});

				// ✅ 加上類型中文說明
				var discountTypeText = coupon.DiscountType == 0 ? "金額折抵" : "百分比折扣";

				return new JsonResult(new
				{
					success = true,
					data = new
					{
						coupon.CouponId,
						coupon.Name,
						coupon.DiscountType,
						discountTypeText,
						coupon.DiscountValue
					}
				});
			}
			catch (Exception ex)
			{
				return new JsonResult(new
				{
					success = false,
					message = $"伺服端發生錯誤：{ex.Message}"
				});
			}
		}

		// ✅ DTO
		public class ApplyCouponRequest
		{
			public string Code { get; set; } = null!;
			public int MemberID { get; set; } = 1;
			public decimal Subtotal { get; set; }
		}
	}
}
