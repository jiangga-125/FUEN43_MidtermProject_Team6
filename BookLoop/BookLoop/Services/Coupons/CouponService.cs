// /Services/Coupons/CouponService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using BookLoop.Models;
using BookLoop.Services.Common;
using BookLoop.Data;

namespace BookLoop.Services.Coupons
{
	public class CouponService : ICouponService
	{
		private readonly MemberContext _db;

		public CouponService(MemberContext db) => _db = db;  // 依賴注入 DbContext

		public async Task<Result<(decimal Discount, string RuleText)>> PreviewDiscountAsync(
	string? couponCode, int memberId, decimal subtotal)
		{
			if (string.IsNullOrWhiteSpace(couponCode))
				return Result<(decimal, string)>.Success((0m, "未輸入優惠券"));

			var coupon = _db.Coupons.FirstOrDefault(x => x.Code == couponCode);
			if (coupon == null) return Result<(decimal, string)>.Fail("優惠券不存在");
			if (!coupon.IsActive) return Result<(decimal, string)>.Fail("優惠券未啟用");

			var now = DateTime.UtcNow;
			if (coupon.StartAt.HasValue && now < coupon.StartAt.Value)
				return Result<(decimal, string)>.Fail("尚未到可用時間");
			if (coupon.EndAt.HasValue && now > coupon.EndAt.Value)
				return Result<(decimal, string)>.Fail("優惠券已過期");

			if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount.Value)
				return Result<(decimal, string)>.Fail($"未達最低消費（需滿 {coupon.MinOrderAmount.Value:#,0} 元）");

			decimal discount = 0m;
			string ruleText = "";

			if (coupon.DiscountType == 0) // 固定金額
			{
				discount = Math.Min(coupon.DiscountValue, subtotal);
				ruleText = $"折抵 {coupon.DiscountValue:#,0} 元";
			}
			else if (coupon.DiscountType == 1) // 百分比折扣
			{
				var rate = (100m - coupon.DiscountValue) / 100m;
				var after = Math.Round(subtotal * rate, 0, MidpointRounding.AwayFromZero);
				discount = subtotal - after;

				if (coupon.MaxDiscountAmount.HasValue)
					discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);

				ruleText = $"折扣 {coupon.DiscountValue}% (最高 {coupon.MaxDiscountAmount ?? 0:#,0} 元)";
			}

			return Result<(decimal, string)>.Success((discount, ruleText));
		}




		public async Task<Result<bool>> MarkAsUsedAsync(
	int orderId, int memberId, string? couponCode, decimal discountApplied)
		{
			if (string.IsNullOrWhiteSpace(couponCode))
				return Result<bool>.Success(true);

			// 先找到優惠券模板
			var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);
			if (coupon == null) return Result<bool>.Fail("找不到優惠券");

			// ✅ 檢查這個會員是否已經用過同一個優惠碼
			bool alreadyUsed = await _db.OrderCouponSnapshots
				.AnyAsync(s => s.CouponID == coupon.CouponId && s.Order.MemberID == memberId);
			if (alreadyUsed)
				return Result<bool>.Fail("此會員已經使用過該優惠券，不能重複使用");

			// ✅ 建立快照，記錄這次用券
			_db.OrderCouponSnapshots.Add(new OrderCouponSnapshot
			{
				OrderID = orderId,
				CouponID = coupon.CouponId,
				CouponNameSnap = coupon.Name,
				CouponTypeSnap = coupon.DiscountType,
				CouponValueSnap = coupon.DiscountValue,
				CouponDiscountAmount = discountApplied,
				CreatedAt = DateTime.UtcNow
			});

			await _db.SaveChangesAsync();
			return Result<bool>.Success(true);
		}


	}
}
