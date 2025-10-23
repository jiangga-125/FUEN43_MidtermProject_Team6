using BookLoop.Services.Common;
using System.Threading.Tasks;

namespace BookLoop.Services.Coupons
{
	public interface ICouponService
	{
		// 驗證並計算折抵金額
		Task<Result<(decimal Discount, string RuleText)>> PreviewDiscountAsync(
	string? couponCode,
	int memberId,
	decimal subtotal
);


		// 下單時標記使用（含快照）
		Task<Result<bool>> MarkAsUsedAsync(
			int orderId,
			int memberId,
			string? couponCode,
			decimal discountAmount
		);
	}
}
