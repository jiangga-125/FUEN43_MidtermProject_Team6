namespace 會員.Services.Pricing
{
	public class PricingInputs
	{
		public int MemberId { get; set; }      // 會員ID（可為匿名=>0，看需求）
		public decimal Subtotal { get; set; }  // 原始小計（尚未套券前）
		public string? CouponCode { get; set; } // 輸入的優惠碼（可為空）
		public int UsePoints { get; set; }      // 想要使用的點數（點數=元）
	}
}
