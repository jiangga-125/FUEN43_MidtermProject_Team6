// /Services/Orders/OrderService.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using BookLoop.Models;
using BookLoop.Services.Common;
using BookLoop.Services.Coupons;
using BookLoop.Services.Orders;
using BookLoop.Services.Points;
using BookLoop.Services.Pricing;
using BookLoop.Data;

namespace BookLoop.Services.Orders
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
				// 1) 建立訂單主檔
				var order = new Order
				{
					MemberID = input.MemberId,
					TotalAmount = preview.Subtotal,
					DiscountAmount = preview.CouponDiscount + preview.PointsUsed,
					CouponDiscountAmount = preview.CouponDiscount, // ★ 優惠券折扣金額
					DiscountCode = input.CouponCode,               // ★ 使用的優惠碼
					Notes = BuildNotes(preview, input),            // ★ 自訂一個方法組合 Notes
					CreatedAt = DateTime.UtcNow
				};
				_db.Orders.Add(order);
				await _db.SaveChangesAsync();

				// 2) 標記優惠券使用 + 快照
				var markCoupon = await _coupon.MarkAsUsedAsync(
					order.OrderID,
					input.MemberId,
					input.CouponCode,
					preview.CouponDiscount
				);
				if (!markCoupon.Ok) return Result<int>.Fail(markCoupon.Message!);

				// 3) 扣點
				if (preview.PointsUsed > 0)
				{
					var deduct = await _points.DeductAsync(
						input.MemberId,
						preview.PointsUsed,
						order.OrderID,
						"USE_FOR_ORDER"
					);
					if (!deduct.Ok) return Result<int>.Fail(deduct.Message!);
				}

				// 4) （若有）發點
				if (preview.PointsEarned > 0)
				{
					var credit = await _points.CreditAsync(
						input.MemberId,
						preview.PointsEarned,
						order.OrderID,
						"EARN_BY_ORDER"
					);
					if (!credit.Ok) return Result<int>.Fail(credit.Message!);
				}

				await tx.CommitAsync(); // 交易提交
				return Result<int>.Success(order.OrderID, "下單成功");
			}
			catch (DbUpdateException ex)
			{
				await tx.RollbackAsync();
				string msg = ex.GetBaseException().Message;

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

		// 🔹 Notes 的組合邏輯抽成一個方法
		private static string BuildNotes(PricingResult preview, PricingInputs input)
		{
			var notes = new List<string>();

			if (!string.IsNullOrEmpty(input.CouponCode) && preview.CouponDiscount > 0)
				notes.Add($"使用優惠券 {input.CouponCode} 折抵 {preview.CouponDiscount} 元");

			if (preview.PointsUsed > 0)
				notes.Add($"使用點數 {preview.PointsUsed} 點");

			if (preview.PointsEarned > 0)
				notes.Add($"獲得點數 {preview.PointsEarned} 點");

			return notes.Count > 0 ? string.Join("，", notes) : "無折扣";
		}

	}
}
