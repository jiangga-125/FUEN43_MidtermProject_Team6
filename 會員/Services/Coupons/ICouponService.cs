// /Services/Coupons/ICouponService.cs
using System.Threading.Tasks;
using 會員.Services.Common;


namespace 會員.Services.Coupons
{
	public interface ICouponService
	{
		// 驗證並計算折抵金額（只做規則判斷與金額計算，不落資料）
		Task<Result<decimal>> PreviewDiscountAsync(
			string? couponCode,           // 優惠碼（可為空）
			int memberId,                 // 會員ID
			decimal subtotal              // 原小計
		);

		// 下單時標記使用（含次數/綁定等，需包在交易內由 OrderService 呼叫）
		Task<Result<bool>> MarkAsUsedAsync(
			int orderId,                  // 訂單ID
			int memberId,
			string? couponCode,
			decimal discountApplied       // 本次折抵金額（寫入快照）
		);
	}
}
