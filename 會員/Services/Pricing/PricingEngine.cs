// /Services/Pricing/PricingEngine.cs
using System;
using System.Threading.Tasks;
using 會員.Services.Coupons;
using 會員.Services.Points;
using 會員.Services.Pricing;

namespace 會員.Services.Pricing
{
	public class PricingEngine : IPricingEngine
	{
		private readonly ICouponService _coupon;
		private readonly IPointsService _points;

		public PricingEngine(ICouponService coupon, IPointsService points)
		{
			_coupon = coupon;
			_points = points;
		}

		public async Task<PricingResult> PreviewAsync(PricingInputs input)
		{
			var result = new PricingResult
			{
				Subtotal = input.Subtotal
			};

			// 1) 先試算優惠券折抵
			var couponRes = await _coupon.PreviewDiscountAsync(input.CouponCode, input.MemberId, input.Subtotal);
			if (!couponRes.Ok)
			{
				result.CouponMessage = couponRes.Message;      // 告知前端為何失敗
				result.CouponDiscount = 0m;
				result.AfterCoupon = input.Subtotal;
			}
			else
			{
				result.CouponDiscount = couponRes.Ok ? couponRes.Data : 0m; // 成功→用 Data；失敗→0
				result.AfterCoupon = Math.Max(0, input.Subtotal - result.CouponDiscount);
			}

			// ==== 2) 計算可用點數（20% 上限 + 餘額 + 用戶想用）====
var maxPoints = Math.Max(0, _points.CalcMaxUsablePoints(result.AfterCoupon)); // 券後 20%
var balance   = await _points.GetBalanceAsync(input.MemberId);                // 會員餘額
var requested = Math.Max(0, input.UsePoints);                                 // 使用者想用（不得 <0）

// 逐項限制：先看上限，再看餘額
var capLimited      = Math.Min(requested, maxPoints); // 受 20% 上限限制後
var balanceLimited  = Math.Min(capLimited, balance);  // 再受餘額限制
result.PointsUsed   = balanceLimited;                 // 最終實際可用
result.Payable      = Math.Max(0, result.AfterCoupon - result.PointsUsed);

// 訊息：把原因講清楚（方便你在前端看到是哪個環節卡住）
var msgs = new List<string>();
if (requested > maxPoints)
    msgs.Add($"受 20% 上限限制：最多可用 {maxPoints} 點（券後 {result.AfterCoupon:#,0} 的 20%）");
if (requested > balance)
    msgs.Add($"點數餘額不足：目前餘額 {balance} 點");
if (requested > 0 && result.PointsUsed == 0 && maxPoints == 0)
    msgs.Add("券後金額過低，20% 上限為 0 點");
result.PointsMessage = msgs.Count > 0 ? string.Join("；", msgs) : null;

			return result;
		}
	}
}
