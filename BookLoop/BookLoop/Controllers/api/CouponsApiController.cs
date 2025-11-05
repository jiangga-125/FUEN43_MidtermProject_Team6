using BookLoop.Services;
using BookLoop.Services.Coupons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookLoop.Data;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

namespace BookLoop.Controllers.api
{
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[Route("api/[controller]/[action]")]
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

		// helper: 從 token claim 取 memberId
		private int? GetMemberIdFromClaims()
		{
			var idClaim = User.FindFirst("memberId")
					   ?? User.FindFirst(ClaimTypes.NameIdentifier)
					   ?? User.FindFirst("sub");
			if (idClaim == null) return null;
			return int.TryParse(idClaim.Value, out var id) ? id : null;
		}

		// ✅ 取得使用者可用優惠券列表
		[HttpGet("list")]
		public IActionResult List()
		{
			var memberId = GetMemberIdFromClaims();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			var list = _couponService.GetAvailableCoupons(memberId.Value);
			return Ok(new { success = true, data = list });
		}

		// ✅ 套用優惠券（預覽折扣金額）
		[HttpPost]
		public async Task<IActionResult> Apply([FromBody] ApplyCouponRequest req)
		{
			// 不信任前端傳的 MemberID -> 由 token 取
			var memberId = GetMemberIdFromClaims();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			// 基本 input 驗證
			if (string.IsNullOrWhiteSpace(req.Code))
				return BadRequest(new { success = false, message = "請輸入優惠碼" });

			// 使用 token 的 memberId（忽略 req.MemberID）
			var result = await _couponService.PreviewDiscountAsync(req.Code.Trim(), memberId.Value, req.Subtotal);

			if (!result.IsSuccess)
				return BadRequest(new { success = false, message = result.ErrorMessage });

			return Ok(new
			{
				success = true,
				data = new
				{
					discount = result.Value.Discount,
					rule = result.Value.RuleText
				}
			});
		}

		// ✅ 根據代碼查優惠券（如果你希望此方法為公開，請加 [AllowAnonymous]）
		[HttpGet]
		public IActionResult GetByCode(string code, decimal? amount = null)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(code))
					return BadRequest(new { success = false, message = "請輸入代碼" });

				var coupon = _context.Coupons
					.Where(c => c.Code == code.Trim())
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
					return NotFound(new { success = false, message = "找不到此優惠券代碼" });

				if (coupon.IsActive == false)
					return BadRequest(new { success = false, message = "此優惠券尚未啟用" });

				// 注意：確保 DB 存的是 UTC 時間，或在此做轉換
				var now = DateTime.UtcNow;
				if (coupon.StartAt.HasValue && now < coupon.StartAt.Value.ToUniversalTime())
					return BadRequest(new { success = false, message = "此優惠券尚未開始" });
				if (coupon.EndAt.HasValue && now > coupon.EndAt.Value.ToUniversalTime())
					return BadRequest(new { success = false, message = "此優惠券已過期" });

				if (coupon.MinOrderAmount.HasValue && amount.HasValue && amount.Value < coupon.MinOrderAmount.Value)
					return BadRequest(new
					{
						success = false,
						message = $"未達最低消費金額 {coupon.MinOrderAmount.Value:N0} 元"
					});

				var discountTypeText = coupon.DiscountType == 0 ? "金額折抵" : "百分比折扣";

				return Ok(new
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
				return StatusCode(500, new
				{
					success = false,
					message = $"伺服端發生錯誤：{ex.Message}"
				});
			}
		}

		// ✅ DTO（移除預設 member id）
		public class ApplyCouponRequest
		{
			public string Code { get; set; } = null!;
			// public int MemberID { get; set; } = 1; // <- 不要讓前端傳作為身份來源
			public decimal Subtotal { get; set; }
		}
	}
}
