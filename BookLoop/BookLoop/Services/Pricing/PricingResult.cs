namespace BookLoop.Services.Pricing
{
	public class PricingResult
	{
		public decimal Subtotal { get; set; }          // 原小計
		public decimal CouponDiscount { get; set; }    // 優惠券折抵金額
		public decimal AfterCoupon { get; set; }       // 套券後金額
		public int PointsUsed { get; set; }            // 實際使用點數（元）
		public decimal Payable { get; set; }           // 應付金額（AfterCoupon - PointsUsed）
		public int PointsEarned { get; set; }          // 此筆訂單發點（若有規則）
		public string? CouponMessage { get; set; }     // 券提示（如達上限、快到期等）
		public string? PointsMessage { get; set; }     // 點數提示（如不足、上限20%等）
	}
}
