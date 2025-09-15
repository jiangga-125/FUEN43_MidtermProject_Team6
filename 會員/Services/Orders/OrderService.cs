// /Services/Orders/OrderService.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using 會員.Models;
using 會員.Services.Common;
using 會員.Services.Coupons;
using 會員.Services.Orders;
using 會員.Services.Points;
using 會員.Services.Pricing;

namespace 會員.Services.Orders
{
	public class OrderService : IOrderService
	{
		private readonly MemberContext _db;
		private readonly IPricingEngine _pricing;
		private readonly ICouponService _coupon;
		private readonly IPointsService _points;

		public OrderService(MemberContext db, IPricingEngine pricing, ICouponService coupon, IPointsService points)
		{
			_db = db;
			_pricing = pricing;
			_coupon = coupon;
			_points = points;
		}

		public Task<PricingResult> PreviewAsync(PricingInputs input)
			=> _pricing.PreviewAsync(input); // 直接委派給定價引擎

		public async Task<Result<int>> PlaceOrderAsync(PricingInputs input)
		{
			// 先跑一遍 Preview，確定要付的金額 & 可用點數
			var preview = await _pricing.PreviewAsync(input);
			if (preview.Payable < 0) return Result<int>.Fail("金額異常");

			using var tx = await _db.Database.BeginTransactionAsync(); // 開啟交易
			try
			{
				// 1) 建立訂單主檔（僅示範必要欄位）
				var order = new Order
				{
					MemberId = input.MemberId,
					TotalAmount = preview.Subtotal,
					DiscountAmount = preview.CouponDiscount + preview.PointsUsed,
					CreatedAt = DateTime.UtcNow
				};
				_db.Orders.Add(order);
				await _db.SaveChangesAsync();

				// 2) 標記優惠券使用 + 快照
				var markCoupon = await _coupon.MarkAsUsedAsync(order.OrderId, input.MemberId, input.CouponCode, preview.CouponDiscount);
				if (!markCoupon.Ok) return Result<int>.Fail(markCoupon.Message!);

				// 3) 扣點
				if (preview.PointsUsed > 0)
				{
					var deduct = await _points.DeductAsync(input.MemberId, preview.PointsUsed, order.OrderId, "USE_FOR_ORDER");
					if (!deduct.Ok) return Result<int>.Fail(deduct.Message!);
				}

				// 4) （若有）發點
				if (preview.PointsEarned > 0)
				{
					var credit = await _points.CreditAsync(input.MemberId, preview.PointsEarned, order.OrderId, "EARN_BY_ORDER");
					if (!credit.Ok) return Result<int>.Fail(credit.Message!);
				}

				await tx.CommitAsync(); // 交易提交
				return Result<int>.Success(order.OrderId, "下單成功");
			}
			catch (DbUpdateException ex)
			{
				await tx.RollbackAsync();

				// 把最內層 SQL 錯誤訊息整理出來
				string msg = ex.GetBaseException().Message; // 最常見就能看到 FK/NOT NULL/Unique 等

				if (ex.InnerException is SqlException sqlEx && sqlEx.Errors?.Count > 0)
				{
					var sb = new System.Text.StringBuilder();
					foreach (SqlError e in sqlEx.Errors)
						sb.AppendLine($"[SQL {e.Number}] {e.Message}");
					msg = sb.ToString();
				}

				return Result<int>.Fail($"資料庫更新失敗：{msg}");
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				return Result<int>.Fail($"下單失敗（非資料庫）：{ex.Message}");
			}
		}
	}
}
