
// 功能說明：
// 此控制器負責會員端優惠券相關 API，包括：
//   ✅ 查詢會員已擁有的優惠券（List）
//   ✅ 查詢可領取的優惠券（Available）
//   ✅ 以按鈕或代碼領取優惠券（Claim / ClaimByCode）
//
// ⚠️ 開發注意事項（2025/11 狀態）
// 目前「會員登入系統」尚未完成 Cookie 登入部分，
// 因此無法從 User.Claims 取得真實的 MemberId。
// 為了讓優惠券功能可以先開發與測試，暫時在程式中使用假會員 ID = 1。
// 之後當登入機制完成（可從 Claims 取得 MemberId）後，
// 請將 GetCurrentMemberId() 方法內的假 ID 移除，改為從登入資訊抓取即可。
//
// TODO: 等會員登入系統完成後，改成以下寫法
// int currentMemberId = int.Parse(User.FindFirst("MemberId").Value);
// 並刪除 fallback 假會員 ID 相關程式碼。


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

		// 🔍 測試誰登入
		[HttpGet("whoami")]
		public IActionResult WhoAmI()
		{
			return Ok(new
			{
				isAuth = User.Identity?.IsAuthenticated,
				user = User.Identity?.Name,
				claims = User.Claims.Select(c => new { c.Type, c.Value })
			});
		}

		// ✅ 統一安全取得登入會員ID（含開發測試用 fallback）
		private int GetCurrentMemberId()
		{
			try
			{
				// 🎯 嘗試從登入資訊取得 MemberID
				var idClaim = User?.FindFirst("MemberID")?.Value;
				if (!string.IsNullOrEmpty(idClaim))
					return int.Parse(idClaim);

				// ⚠️ 若登入系統尚未完成，暫時使用假會員 ID 進行開發測試
				int fakeMemberId = 1;
				Console.WriteLine("⚠️ 尚未登入會員，使用假會員 ID = 1（開發測試用）");
				return fakeMemberId;
			}
			catch
			{
				// 防呆保險：永不拋例外
				return 1;
			}
		}

		// ✅ 取得會員優惠券清單
		[HttpGet]
		public IActionResult List()
		{
			int memberId = GetCurrentMemberId();
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

			var usable = all
				.Where(c => c.Status == 0 && c.StartAt <= now && c.EndAt >= now)
				.OrderBy(c => c.EndAt)
				.ToList();

			var used = all
				.Where(c => c.Status == 1)
				.OrderByDescending(c => c.EndAt)
				.ToList();

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
			int memberId = GetCurrentMemberId();
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
			int memberId = GetCurrentMemberId();

			var coupon = _db.Coupons.FirstOrDefault(c => c.CouponId == req.CouponID);
			if (coupon == null)
				return BadRequest(new { message = "優惠券不存在" });

			bool alreadyClaimed = _db.MemberCoupons.Any(mc => mc.MemberId == memberId && mc.CouponId == req.CouponID);
			if (alreadyClaimed)
				return BadRequest(new { message = "你已經領取過這張優惠券" });

			_db.MemberCoupons.Add(new MemberCoupon
			{
				MemberId = memberId,
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
			int memberId = GetCurrentMemberId();
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
				MemberId = memberId,
				CouponId = coupon.CouponId,
				Status = 0,
				AssignedAt = now
			});
			_db.SaveChanges();

			return Ok(new { message = $"成功領取「{coupon.Name}」優惠券！" });
		}
	}
}
