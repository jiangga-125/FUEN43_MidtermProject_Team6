// /ViewModels/CheckoutPreviewVm.cs
namespace 會員.ViewModels
{
	public class CheckoutPreviewVm
	{
		public int MemberId { get; set; }      // 會員ID
		public decimal Subtotal { get; set; }  // 小計
		public string? CouponCode { get; set; }// 優惠碼
		public int UsePoints { get; set; }     // 想用的點數
	}
}


