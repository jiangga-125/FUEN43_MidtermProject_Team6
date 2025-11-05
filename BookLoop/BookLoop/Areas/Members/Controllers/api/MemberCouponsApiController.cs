using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace BookLoop.Areas.Members.Controllers
{
	public class ClaimCouponRequest
	{
		public int CouponID { get; set; }
	}

	public class ClaimByCodeRequest
	{
		public string Code { get; set; } = "";
	}
	[AllowAnonymous]
	[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Member")]
	[Area("Members")]
	[Route("api/[area]/[controller]/[action]")]
	[ApiController]

	public class MemberCouponsApiController : ControllerBase
	{
		private readonly MemberContext _db;

		public MemberCouponsApiController(MemberContext db)
		{
			_db = db;
		}

		[HttpGet("whoami")]
		public IActionResult WhoAmI()
		{
			return Ok(new
			{
				user = User.Identity?.Name,
				roles = User.Claims
							.Where(c => c.Type == ClaimTypes.Role)
							.Select(c => c.Value)
			});
		}

		// ✅ 統一安全取得登入會員ID
		private int? GetMemberId()
		{
			var idClaim = User?.FindFirst("MemberID")?.Value;
			if (string.IsNullOrEmpty(idClaim))
				return null;
			return int.Parse(idClaim);
		}

		// ✅ 取得會員優惠券清單
		[HttpGet]
		public IActionResult List()
		{
			var memberId = GetMemberId();
			if (memberId == null)
				return Unauthorized(new { message = "尚未登入或找不到會員資訊" });

			var now = DateTime.Now;

			var coupons = from mc in _db.MemberCoupons
						  join c in _db.Coupons on mc.CouponId equals c.CouponId
						  where mc.MemberId == memberId
						  select new
						  {
							  mc.MemberCouponId,
							  c.Name,
							  c.DiscountType,
							  c.DiscountValue,
							  c.StartAt,
							  c.EndAt,
							  mc.Status
						  };

			var all = coupons.ToList();

			// 🟢 可使用
			var usable = all
				.Where(c => c.Status == 0 && c.StartAt <= now && c.EndAt >= now)
				.OrderBy(c => c.EndAt)
				.ToList();

			// 🟡 已使用
			var used = all
				.Where(c => c.Status == 1)
				.OrderByDescending(c => c.EndAt)
				.ToList();

			// 🔴 已過期
			var expired = all
				.Where(c => c.Status == 0 && c.EndAt < now)
				.OrderByDescending(c => c.EndAt)
				.ToList();

			return Ok(new { usable, used, expired });
		}

		// ✅ 取得可領取的優惠券（尚未領取）
		[HttpGet]
		public IActionResult Available()
		{
			var memberId = GetMemberId();
			if (memberId == null)
				return Unauthorized(new { message = "尚未登入或找不到會員資訊" });

			var now = DateTime.Now;

			var claimedIds = _db.MemberCoupons
				.Where(mc => mc.MemberId == memberId)
				.Select(mc => mc.CouponId)
				.ToList();

			var available = _db.Coupons
				.Where(c => c.IsActive &&
							(!c.StartAt.HasValue || c.StartAt <= now) &&
							(!c.EndAt.HasValue || c.EndAt >= now) &&
							!claimedIds.Contains(c.CouponId))
				.Select(c => new
				{
					c.CouponId,
					c.Name,
					c.DiscountType,
					c.DiscountValue,
					c.StartAt,
					c.EndAt
				})
				.ToList();

			return Ok(available);
		}

		// ✅ 領取按鈕
		[HttpPost]
		public IActionResult Claim([FromBody] ClaimCouponRequest req)
		{
			var memberId = GetMemberId();
			if (memberId == null)
				return Unauthorized(new { message = "尚未登入或找不到會員資訊" });

			var coupon = _db.Coupons.FirstOrDefault(c => c.CouponId == req.CouponID);
			if (coupon == null)
				return BadRequest(new { message = "優惠券不存在" });

			bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId && mc.CouponId == req.CouponID);
			if (alreadyClaimed)
				return BadRequest(new { message = "你已經領取過這張優惠券" });

			_db.MemberCoupons.Add(new MemberCoupon
			{
				MemberId = memberId.Value,
				CouponId = req.CouponID,
				Status = 0,
				AssignedAt = DateTime.Now
			});
			_db.SaveChanges();

			return Ok(new { message = "領取成功" });
		}

		// ✅ 使用代碼領取
		[HttpPost]
		public IActionResult ClaimByCode([FromBody] ClaimByCodeRequest req)
		{
			var memberId = GetMemberId();
			if (memberId == null)
				return Unauthorized(new { message = "尚未登入或找不到會員資訊" });

			var code = req.Code?.Trim();
			if (string.IsNullOrWhiteSpace(code))
				return BadRequest(new { message = "請輸入優惠代碼" });

			var coupon = _db.Coupons.FirstOrDefault(c => c.Code == code && c.IsActive);
			if (coupon == null)
				return BadRequest(new { message = "找不到此優惠代碼或已失效" });

			var now = DateTime.Now;
			if (coupon.StartAt.HasValue && now < coupon.StartAt.Value)
				return BadRequest(new { message = "此優惠券尚未開始" });
			if (coupon.EndAt.HasValue && now > coupon.EndAt.Value)
				return BadRequest(new { message = "此優惠券已過期" });

			bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId && mc.CouponId == coupon.CouponId);
			if (alreadyClaimed)
				return BadRequest(new { message = "你已經領取過這張優惠券" });

			_db.MemberCoupons.Add(new MemberCoupon
			{
				MemberId = memberId.Value,
				CouponId = coupon.CouponId,
				Status = 0,
				AssignedAt = now
			});
			_db.SaveChanges();

			return Ok(new { message = $"成功領取「{coupon.Name}」優惠券！" });
		}
	}
}
