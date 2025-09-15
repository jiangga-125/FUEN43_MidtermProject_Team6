// /Services/Coupons/CouponService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using 會員.Models;
using 會員.Services.Common;

namespace 會員.Services.Coupons
{
	public class CouponService : ICouponService
	{
		private readonly MemberContext _db;

		public CouponService(MemberContext db) => _db = db;  // 依賴注入 DbContext

		public async Task<Result<decimal>> PreviewDiscountAsync(string? couponCode, int memberId, decimal subtotal)
		{
			// 如果沒輸入優惠碼，直接回 0 折抵
			if (string.IsNullOrWhiteSpace(couponCode))
				return Result<decimal>.Success(0m);

			// 讀取優惠券
			var c = await _db.Coupons.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Code == couponCode);
			if (c == null) return Result<decimal>.Fail("優惠券不存在");
			if (!c.IsActive) return Result<decimal>.Fail("優惠券未啟用");

			var now = DateTime.Now;
			if (c.StartAt.HasValue && now < c.StartAt.Value)
				return Result<decimal>.Fail("尚未到可用時間");
			if (c.EndAt.HasValue && now > c.EndAt.Value)
				return Result<decimal>.Fail("優惠券已過期");

			// 檢查最低消費
			if (c.MinOrderAmount.HasValue && subtotal < c.MinOrderAmount.Value)
				return Result<decimal>.Fail($"未達最低消費（需滿 {c.MinOrderAmount.Value:#,0} 元）");

			// TODO: 檢查總使用上限 / 每會員上限（依你的欄位實作）
			// ex: 用 MemberCoupons 或 Usage 計數表來判斷

			// 計算折抵
			decimal discount = 0m;
			if (c.DiscountType == 0) // 固定金額
			{
				discount = Math.Min(c.DiscountValue, subtotal); // 折抵不可超過小計
			}
			else if (c.DiscountType == 1) // 折扣%
			{
				// DiscountValue=10 => 打9折 (90%)
				var rate = (100m - c.DiscountValue) / 100m; // 保留清楚語意
				var after = Math.Round(subtotal * rate, 0, MidpointRounding.AwayFromZero);
				discount = subtotal - after;

				if (c.MaxDiscountAmount.HasValue)
					discount = Math.Min(discount, c.MaxDiscountAmount.Value);
			}

			return Result<decimal>.Success(discount);
		}

		public async Task<Result<bool>> MarkAsUsedAsync(int orderId, int memberId, string? couponCode, decimal discountApplied)
		{
			if (string.IsNullOrWhiteSpace(couponCode))
				return Result<bool>.Success(true); // 無券則視為成功

			var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Code == couponCode);
			if (c == null) return Result<bool>.Fail("找不到優惠券");

			// 寫入快照（保持歷史一致性）
			_db.OrderCouponSnapshots.Add(new OrderCouponSnapshot
			{
				OrderId = orderId,                     // 對應 OrderId 欄位
				CouponId = c.CouponId,                 // 你的資料表有 CouponId，建議一起存
				CouponNameSnap = c.Name,               // 對應 CouponNameSnap
				CouponTypeSnap = c.DiscountType,       // 對應 CouponTypeSnap
				CouponValueSnap = c.DiscountValue,     // 對應 CouponValueSnap
				CouponDiscountAmount = discountApplied,// 對應 CouponDiscountAmount
				CreatedAt = DateTime.UtcNow            // 對應 CreatedAt
			});


			// TODO: 在此更新使用計數 / 綁定關係（如 MemberCoupon）等
			// ex: c.TotalUsed++ 或插入 MemberCoupon 使用紀錄

			await _db.SaveChangesAsync();
			return Result<bool>.Success(true);
		}
	}
}
