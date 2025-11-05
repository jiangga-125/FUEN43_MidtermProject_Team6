
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

	// 支援 Cookie 與 JWT，都需要是 Member 角色
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
		public IActionResult WhoAmI()
		{
			return Ok(new
			{
				isAuth = User.Identity?.IsAuthenticated,
				user = User.Identity?.Name,
				claims = User.Claims.Select(c => new { c.Type, c.Value })
			});
		}

		// ✅ 統一安全取得登入會員ID（開發環境才會 fallback 假 ID）
		private int? GetCurrentMemberId()
		{
			// 優先從常見的 claim key 取
			string?[] keys = new[]
			{
				"memberId", "MemberId", "memberID", "MemberID",
				ClaimTypes.NameIdentifier, "sub"
			};

			foreach (var k in keys)
			{
				var claim = User?.FindFirst(k);
				if (claim != null && int.TryParse(claim.Value, out var id))
					return id;
			}

			// 如果是開發環境，允許 fallback 假 ID（方便本地開發）
			if (_env.IsDevelopment())
			{
				Console.WriteLine("⚠️ Development mode: 使用假會員 ID = 1（僅供開發測試）");
				return 1;
			}

			// 非開發環境不予 fallback
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
							  c.Name,
							  c.DiscountType,
							  c.DiscountValue,
							  c.StartAt,
							  c.EndAt,
							  mc.Status
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
			catch (Exception ex)
			{
				// 開發時顯示內部錯誤詳情，方便 debug（上線前改回簡短訊息或記錄到 logger）
				var inner = ex.InnerException?.Message ?? ex.Message;
				return StatusCode(500, new { success = false, message = $"儲存發生錯誤：{inner}", detail = ex.ToString() });
			}

			return Ok(new { message = $"成功領取「{coupon.Name}」優惠券！" });
		}

	}
}