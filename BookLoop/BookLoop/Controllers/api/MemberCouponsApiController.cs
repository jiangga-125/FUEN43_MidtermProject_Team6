using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Security.Claims;

namespace BookLoop.Controllers.api
{
	public class ClaimCouponRequest
	{
		public int CouponID { get; set; }
	}
	public class ClaimByCodeRequest
	{
		public string Code { get; set; } = "";
	}

	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[Route("api/[controller]/[action]")]
	[ApiController]
	public class MemberCouponsApiController : ControllerBase
	{
		private readonly MemberContext _db;
		private readonly IHostEnvironment _env;

		public MemberCouponsApiController(MemberContext db, IHostEnvironment env)
		{
			_db = db;
			_env = env;
		}

		// 🔍 測試誰登入
		[HttpGet("whoami")]
		[AllowAnonymous]
		public IActionResult WhoAmI()
		{
			return Ok(new
			{
				isAuth = User.Identity?.IsAuthenticated,
				user = User.Identity?.Name,
				claims = User.Claims.Select(c => new { c.Type, c.Value })
			});
		}

		// 取得登入會員ID
		private int? GetCurrentMemberId()
		{
			// 優先從常見的 claim key 取
			string?[] keys = new[] {
			"mid", "memberId", "MemberId", "memberID", "MemberID",
			ClaimTypes.NameIdentifier, "sub", "id", "nameid"
			};


			foreach (var k in keys)
			{
				var claim = User?.FindFirst(k);
				if (claim != null && int.TryParse(claim.Value, out var id))
					return id;
			}

			return null;
		}


		// ✅ 取得會員優惠券清單
		[HttpGet]
		public IActionResult List()
		{
			var memberId = GetCurrentMemberId();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			// 將 now 改為本地時間（因為 DB 存的是 local datetime）
			//var now = DateTime.UtcNow;
			var nowLocal = DateTime.Now;

			var coupons = from mc in _db.MemberCoupons
						  join c in _db.Coupons on mc.CouponId equals c.CouponId
						  where mc.MemberId == memberId.Value
						  select new
						  {
							  mc.MemberCouponId,
							  c.CouponId,
							  c.Name,
							  c.DiscountType,
							  c.DiscountValue,
							  c.StartAt,
							  c.EndAt,
							  mc.Status,
							  mc.AssignedAt,
							  mc.IsUsed,
							  mc.UsedAt
						  };

			var all = coupons.ToList();

			var usable = all
			.Where(c => c.Status == 0 &&
				(!c.StartAt.HasValue || c.StartAt.Value <= nowLocal) &&
				(!c.EndAt.HasValue || c.EndAt.Value >= nowLocal))
			.OrderBy(c => c.EndAt)
			.ToList();

			var used = all
				.Where(c => c.Status == 1)
				.OrderByDescending(c => c.EndAt)
				.ToList();

			var expired = all
			.Where(c => c.Status == 0 && c.EndAt.HasValue && c.EndAt.Value < nowLocal)
			.OrderByDescending(c => c.EndAt)
			.ToList();

			return Ok(new { usable, used, expired });
		}

		// ✅ 取得可領取的優惠券（尚未領取）
		[HttpGet]
		public IActionResult Available()
		{
			var memberId = GetCurrentMemberId();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			// 用本地時間比較（因為你的 Coupons 欄位是 local datetime）
			var now = DateTime.UtcNow;
			var nowLocal = DateTime.Now;

			var claimedIds = _db.MemberCoupons
				.Where(mc => mc.MemberId == memberId.Value)
				.Select(mc => mc.CouponId)
				.ToList();

			var available = _db.Coupons
				.Where(c => c.IsActive
				&& (!c.StartAt.HasValue || c.StartAt.Value <= nowLocal)
				&& (!c.EndAt.HasValue || c.EndAt.Value >= nowLocal)
				&& !claimedIds.Contains(c.CouponId))
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
			var memberId = GetCurrentMemberId();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			var coupon = _db.Coupons.FirstOrDefault(c => c.CouponId == req.CouponID);
			if (coupon == null)
				return BadRequest(new { success = false, message = "優惠券不存在" });

			bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId.Value && mc.CouponId == req.CouponID);
			if (alreadyClaimed)
				return BadRequest(new { success = false, message = "你已經領取過這張優惠券" });

			var mcNew = new MemberCoupon
			{
				MemberId = memberId.Value,
				CouponId = req.CouponID,
				Status = 0,
				AssignedAt = DateTime.UtcNow
			};

			try
			{
				_db.MemberCoupons.Add(mcNew);
				_db.SaveChanges();
			}
			catch (Exception ex)
			{
				// 真實專案可改成記錄到 logger
				return StatusCode(500, new { success = false, message = $"儲存發生錯誤：{ex.Message}" });
			}

			return Ok(new { success = true, message = "領取成功" });
		}

		// ✅ 使用代碼領取
		//[HttpPost]
		//public IActionResult ClaimByCode([FromBody] ClaimByCodeRequest req)
		//{
		//	var memberId = GetCurrentMemberId();
		//	if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

		//	var code = req.Code?.Trim();
		//	if (string.IsNullOrWhiteSpace(code))
		//		return BadRequest(new { success = false, message = "請輸入優惠代碼" });

		//	var coupon = _db.Coupons.FirstOrDefault(c => c.Code == code && c.IsActive);
		//	if (coupon == null)
		//		return BadRequest(new { success = false, message = "找不到此優惠代碼或已失效" });

		//	var now = DateTime.UtcNow;
		//	if (coupon.StartAt.HasValue && now < coupon.StartAt.Value.ToUniversalTime())
		//		return BadRequest(new { success = false, message = "此優惠券尚未開始" });
		//	if (coupon.EndAt.HasValue && now > coupon.EndAt.Value.ToUniversalTime())
		//		return BadRequest(new { success = false, message = "此優惠券已過期" });

		//	bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId.Value && mc.CouponId == coupon.CouponId);
		//	if (alreadyClaimed)
		//		return BadRequest(new { success = false, message = "你已經領取過這張優惠券" });

		//	var mcNew = new MemberCoupon
		//	{
		//		MemberId = memberId.Value,
		//		CouponId = coupon.CouponId,
		//		Status = 0,
		//		AssignedAt = now
		//	};

		//	try
		//	{
		//		_db.MemberCoupons.Add(mcNew);
		//		_db.SaveChanges();
		//	}
		//	catch (Exception ex)
		//	{
		//		return StatusCode(500, new { success = false, message = $"儲存發生錯誤：{ex.Message}" });
		//	}

		//	return Ok(new { success = true, message = $"成功領取「{coupon.Name}」優惠券！" });
		//}
		[HttpPost]
		public IActionResult ClaimByCode([FromBody] ClaimByCodeRequest req)
		{
			var memberId = GetCurrentMemberId();
			if (memberId == null) return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

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

			bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId.Value && mc.CouponId == coupon.CouponId);
			if (alreadyClaimed)
				return BadRequest(new { message = "你已經領取過這張優惠券" });

			var mc = new MemberCoupon
			{
				MemberId = memberId.Value,
				CouponId = coupon.CouponId,
				Status = 0,
				AssignedAt = now
			};

			try
			{
				_db.MemberCoupons.Add(mc);
				_db.SaveChanges();
			}
			catch (DbUpdateException dbEx)
			{
				// 若是唯一索引衝突（已被別人領或 race），回傳友善訊息
				var inner = dbEx.InnerException?.Message ?? dbEx.Message;
				if (inner?.Contains("UX_MemberCoupons_Member_Coupon") == true || inner?.Contains("UNIQUE KEY") == true)
					return BadRequest(new { success = false, message = "你已經領取過這張優惠券" });

				return StatusCode(500, new { success = false, message = $"儲存發生錯誤：{inner}" });
			}

			return Ok(new { message = $"成功領取「{coupon.Name}」優惠券！" });
		}

	}
}