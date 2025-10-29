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
		/// 產生綠界自動送出的表單
		/// </summary>
		public static string GeneratePostForm(ECPayRequest model)
		{
			// 先生成 CheckMacValue
			model.CheckMacValue = GenerateCheckMacValue(model);

			var formBuilder = new StringBuilder();
			formBuilder.AppendLine($"<form id='ecpayForm' method='post' action='{GatewayUrl}'>");

			// 使用反射生成表單欄位
			foreach (var prop in model.GetType().GetProperties())
			{
				var value = prop.GetValue(model, null);
				if (value != null)
				{
					formBuilder.AppendLine($"<input type='hidden' name='{prop.Name}' value='{HttpUtility.HtmlEncode(value.ToString())}' />");
				}
			}

			// 固定欄位
			formBuilder.AppendLine("<input type='hidden' name='PaymentType' value='aio' />");

			formBuilder.AppendLine("</form>");
			formBuilder.AppendLine("<script>document.getElementById('ecpayForm').submit();</script>");

			Console.WriteLine("---------- ECPay Form Debug ----------");
			Console.WriteLine(formBuilder.ToString());
			Console.WriteLine("------------------------------------------------");

			return formBuilder.ToString();
		}

		/// <summary>
		/// 產生 CheckMacValue
		/// </summary>
		public static string GenerateCheckMacValue(ECPayRequest model)
		{
			// 將 model 屬性轉成 Dictionary（排除 CheckMacValue）
			var parameters = model.GetType()
				.GetProperties()
				.Where(p => p.GetValue(model) != null && p.Name != "CheckMacValue")
				.ToDictionary(p => p.Name, p => p.GetValue(model).ToString());

			// 確保 PaymentType 固定存在
			if (!parameters.ContainsKey("PaymentType"))
				parameters.Add("PaymentType", "aio");

			// 參數排序
			var sortedParams = parameters.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();

			// 組成原始字串
			var raw = $"HashKey={HashKey}&{string.Join("&", sortedParams.Select(x => $"{x.Key}={x.Value}"))}&HashIV={HashIV}";

	

			// 替換特殊字元（綠界官方建議）
			var urlEncoded = HttpUtility.UrlEncode(raw).ToLower();

			// ✅ 將空格換成 %20
			urlEncoded = urlEncoded.Replace("+", "%20");

			// 替換其他特殊字元
			urlEncoded = urlEncoded
				.Replace("%2d", "-")
				.Replace("%5f", "_")
				.Replace("%2e", ".")
				.Replace("%21", "!")
				.Replace("%2a", "*")
				.Replace("%28", "(")
				.Replace("%29", ")");

			// SHA256 加密
			using (var sha256 = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(urlEncoded);
				var hash = sha256.ComputeHash(bytes);
				var checkMac = BitConverter.ToString(hash).Replace("-", "").ToUpper();

				// 🔹 Debug Log 保留
				Console.WriteLine("---------- ECPay CheckMacValue Debug ----------");
				Console.WriteLine($"RAW : {raw}");
				Console.WriteLine($"Encoded : {urlEncoded}");
				Console.WriteLine($"CheckMacValue : {checkMac}");
				Console.WriteLine("------------------------------------------------");

				return checkMac;
			}
		}

		/// <summary>
		/// 驗證綠界回傳通知
		/// </summary>
		public static bool VerifyNotification(ECPayRequest request)
		{
			var expected = GenerateCheckMacValue(request);
			var isValid = string.Equals(expected, request.CheckMacValue, StringComparison.OrdinalIgnoreCase);

			// 🔹 Debug Log 保留
			Console.WriteLine("---------- ECPay Verify Debug ----------");
			Console.WriteLine($"Expected : {expected}");
			Console.WriteLine($"Received : {request.CheckMacValue}");
			Console.WriteLine($"Result : {(isValid ? "✅ Valid" : "❌ Invalid")}");
			Console.WriteLine("------------------------------------------------");

			return isValid;
		}
	}
}