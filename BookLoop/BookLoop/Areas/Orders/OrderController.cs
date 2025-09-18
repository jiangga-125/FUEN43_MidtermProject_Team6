using BookLoop.Models;
using BookLoop.Services.Coupons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Orders
{
	public class OrderController : Controller
	{
		private readonly MemberContext _db;   // ★ 宣告 _context
		private readonly ICouponService _couponService;

		public OrderController(MemberContext db, ICouponService couponService)   // ★ 透過建構子注入
		{
			_db = db;
			_couponService = couponService;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateOrder(int orderId, string couponCode, int memberId, int usePoints)
		{
			var order = await _db.Orders.FindAsync(orderId);
			if (order == null)
			{
				TempData["Error"] = "找不到訂單";
				return RedirectToAction("Index");
			}

			// 1. 驗證優惠券
			var preview = await _couponService.PreviewDiscountAsync(couponCode, memberId, order.TotalAmount);
			if (!preview.Ok)
			{
				TempData["Error"] = preview.Message;
				return RedirectToAction("Index");
			}

			var discount = preview.Data.Discount;    // 折扣金額
			var ruleText = preview.Data.RuleText;    // 規則文字

			// 2. 更新訂單欄位
			order.DiscountCode = couponCode;       // 使用的優惠券代碼
			order.DiscountAmount = discount;       // 總折抵金額
			order.CouponDiscountAmount = discount; // 優惠券折抵金額
			order.TotalAmount -= discount;         // 更新金額
			order.Notes = $"使用優惠券 {couponCode}，{ruleText}，折抵 {discount} 元"; // 備註


			// 3. 寫入優惠券快照
			var memberCoupon = await _db.MemberCoupons
				.Include(mc => mc.Coupon)
				.FirstOrDefaultAsync(mc => mc.MemberId == memberId && mc.Coupon.Code == couponCode);

			if (memberCoupon != null)
			{
				order.MemberCouponId = memberCoupon.MemberCouponId;         // 關聯會員券
				order.CouponTypeSnap = memberCoupon.Coupon.DiscountType;    // 快照券類型
				order.CouponValueSnap = memberCoupon.Coupon.DiscountValue;  // 快照券數值
				order.CouponNameSnap = memberCoupon.Coupon.Name;            // 快照券名稱
				order.CouponDiscountAmount = discount;                      // 快照折抵金額
			}

			Console.WriteLine($"DEBUG: Notes={order.Notes}, DiscountCode={order.DiscountCode}, DiscountAmount={order.DiscountAmount}");

			// 4. 存檔

			// 4. 存檔
			await _db.SaveChangesAsync();

			TempData["Msg"] = "訂單建立成功！";
			return RedirectToAction("Index");
		}


	}
}
