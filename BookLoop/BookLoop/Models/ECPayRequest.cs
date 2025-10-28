using System;

namespace BookLoop.Models
{
	public class ECPayRequest
	{
		// === 基本交易欄位 - 送出用 ===
		public string MerchantID { get; set; } = "3002607";   // 測試商店代號
		public string MerchantTradeNo { get; set; }           // 商店訂單編號（需唯一）
		public string MerchantTradeDate { get; set; }         // 訂單建立時間（格式：yyyy/MM/dd HH:mm:ss）
		public int TotalAmount { get; set; }                  // 訂單金額
		public string TradeDesc { get; set; } = "BookLoop 訂單付款"; // 交易描述
		public string ItemName { get; set; }                  // 商品名稱
		public string ReturnURL { get; set; }                 // 綠界付款完成通知
		public string OrderResultURL { get; set; }            // 使用者付款完成導回頁面
		public string ChoosePayment { get; set; } = "ALL";   // 付款方式
		public string EncryptType { get; set; } = "1";       // 固定為 SHA256

		// 🔹 固定欄位
		public string PaymentType { get; set; } = "aio";     // 固定 aio

		// === 回傳通知用欄位 ===
		public string RtnCode { get; set; }
		public string RtnMsg { get; set; }
		public string TradeNo { get; set; }
		public string TradeAmt { get; set; }
		public string PaymentDate { get; set; }
		public string PaymentTypeChargeFee { get; set; }
		public string TradeDate { get; set; }
		public string SimulatePaid { get; set; }

		// === 檢查碼 ===
		public string CheckMacValue { get; set; }
	}
}