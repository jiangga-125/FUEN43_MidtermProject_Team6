using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using BookLoop.Models;

namespace BookLoop.Helpers
{
	public static class ECPayHelper
	{
		private const string HashKey = "pwFHCqoQZGmho4w6";
		private const string HashIV = "EkRm7iFT261dpevs";
		private const string GatewayUrl = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

		/// <summary>
		/// 產生綠界送出的表單
		/// </summary>
		public static string GeneratePostForm(ECPayRequest model, bool autoSubmit = false, string formId = "ecpayForm")
		{
			// 先生成 CheckMacValue
			model.CheckMacValue = GenerateCheckMacValue(model);

			var submitFields = new string[]
			{
				"MerchantID","MerchantTradeNo","MerchantTradeDate","PaymentType",
				"TotalAmount","TradeDesc","ItemName","ReturnURL",
				"OrderResultURL","ChoosePayment","EncryptType","CheckMacValue"
			};

			var sb = new StringBuilder();
			sb.AppendLine($"<form id='{formId}' method='post' action='{GatewayUrl}'>");

			foreach (var prop in model.GetType().GetProperties())
			{
				if (!submitFields.Contains(prop.Name)) continue;
				var value = prop.GetValue(model, null);
				if (value != null)
				{
					sb.AppendLine($"<input type='hidden' name='{prop.Name}' value='{HttpUtility.HtmlEncode(value.ToString())}' />");
				}
			}

			// autoSubmit = false，給使用者按鈕
			if (!autoSubmit)
				sb.AppendLine("<button type='submit'>前往綠界付款</button>");

			sb.AppendLine("</form>");

			// ✅ 如果 autoSubmit = true，可以在外部 JS 觸發 submit
			// 注意：不要用 inline script 避免 CSP 阻擋
			return sb.ToString();
		}

		/// <summary>
		/// 產生 CheckMacValue
		/// </summary>
		public static string GenerateCheckMacValue(ECPayRequest model)
		{
			var parameters = new Dictionary<string, string>
			{
			{ "MerchantID", model.MerchantID ?? "" },
				{ "MerchantTradeNo", model.MerchantTradeNo ?? "" },
				{ "MerchantTradeDate", model.MerchantTradeDate ?? "" },
				{ "PaymentType", "aio" },
				{ "TotalAmount", model.TotalAmount.ToString() ?? "0" },
				{ "TradeDesc", model.TradeDesc ?? "" },
				{ "ItemName", model.ItemName ?? "" },
				{ "ReturnURL", model.ReturnURL ?? "" },
				{ "OrderResultURL", model.OrderResultURL ?? "" },
				{ "ChoosePayment", model.ChoosePayment ?? "" },
				{ "EncryptType", "1" }
			};

			var sorted = parameters.OrderBy(x => x.Key);
			var raw = $"HashKey={HashKey}&{string.Join("&", sorted.Select(x => $"{x.Key}={x.Value}"))}&HashIV={HashIV}";
			var encoded = HttpUtility.UrlEncode(raw, Encoding.UTF8).ToLower();

			using (var sha256 = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(encoded);
				var hash = sha256.ComputeHash(bytes);
				return BitConverter.ToString(hash).Replace("-", "").ToUpper();
			}
		}
		/// <summary>
		/// 驗證綠界回傳通知
		/// </summary>
		public static bool VerifyNotification(ECPayRequest request)
		{
			var expected = GenerateCheckMacValue(request);
			return string.Equals(expected, request.CheckMacValue, StringComparison.OrdinalIgnoreCase);
		}
	}
}