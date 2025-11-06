using BookLoop.Data;
using BookLoop.Models;
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

		// 🔍 取得目前登入會員 ID（支援多種 claim 名稱）
		private int? GetCurrentMemberId()
		{
			string?[] keys = new[]
			{
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

		// ✅ 顯示該會員目前擁有的優惠券清單
		[HttpGet]
		public IActionResult List()
		{
			var memberId = GetCurrentMemberId();
			if (memberId == null)
				return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

			var now = DateTime.Now;

			// 🔍 會員已領取的優惠券（JOIN Coupon）
			var coupons = (from mc in _db.MemberCoupons.AsNoTracking()
						   join c in _db.Coupons.AsNoTracking() on mc.CouponId equals c.CouponId
						   where mc.MemberId == memberId.Value
						   select new
						   {
							   mc.MemberCouponId,
							   c.CouponId,
							   Name = c.Name ?? "(未命名優惠券)",
							   Code = c.Code ?? "",
							   c.DiscountType,
							   c.DiscountValue,
							   StartAt = c.StartAt.HasValue ? c.StartAt.Value.ToString("yyyy/MM/dd") : null,
							   EndAt = c.EndAt.HasValue ? c.EndAt.Value.ToString("yyyy/MM/dd") : null,
							   mc.Status,
							   mc.AssignedAt,
							   mc.IsUsed,
							   mc.UsedAt
						   }).ToList();

			// 🔹 可用
			var usable = coupons
				.Where(c => c.Status == 0 &&
					(string.IsNullOrEmpty(c.StartAt) || DateTime.Parse(c.StartAt) <= now) &&
					(string.IsNullOrEmpty(c.EndAt) || DateTime.Parse(c.EndAt) >= now))
				.OrderBy(c => c.EndAt)
				.ToList();

			// 🔹 已使用
			var used = coupons
				.Where(c => c.Status == 1)
				.OrderByDescending(c => c.EndAt)
				.ToList();

			// 🔹 已過期
			var expired = coupons
				.Where(c => c.Status == 0 && !string.IsNullOrEmpty(c.EndAt) && DateTime.Parse(c.EndAt) < now)
				.OrderByDescending(c => c.EndAt)
				.ToList();

			return Ok(new { usable, used, expired });
		}


		// 🚫 停用「可直接領取」功能
		[HttpGet]
		public IActionResult Available()
		{
			// 固定回傳空清單，避免前端誤顯示
			return Ok(new { success = true, available = new object[0] });
		}

		// 🚫 停用「直接領取按鈕」
		[HttpPost]
		public IActionResult Claim()
		{
			return BadRequest(new { success = false, message = "目前僅支援輸入代碼領取優惠券" });
		}

		// ✅ 使用代碼領取優惠券（唯一入口）
		[HttpPost]
		public IActionResult ClaimByCode([FromBody] ClaimByCodeRequest req)
		{
			var memberId = GetCurrentMemberId();
			if (memberId == null)
				return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

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

			// 是否已經領取過
			bool alreadyClaimed = _db.MemberCoupons.Any(mc =>
				mc.MemberId == memberId.Value && mc.CouponId == coupon.CouponId);

			if (alreadyClaimed)
				return BadRequest(new { message = "你已經領取過這張優惠券" });

			// ✅ 新增到會員優惠券表
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
				var inner = dbEx.InnerException?.Message ?? dbEx.Message;
				if (inner?.Contains("UX_MemberCoupons_Member_Coupon") == true ||
					inner?.Contains("UNIQUE KEY") == true)
					return BadRequest(new { success = false, message = "你已經領取過這張優惠券" });

				return StatusCode(500, new { success = false, message = $"儲存發生錯誤：{inner}" });
			}

			return Ok(new { message = $"成功領取「{coupon.Name}」優惠券！" });
		}
	}
}
