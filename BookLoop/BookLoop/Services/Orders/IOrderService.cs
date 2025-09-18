﻿// /Services/Orders/IOrderService.cs
using System.Threading.Tasks;
using BookLoop.Services.Common;
using BookLoop.Services.Pricing;


namespace BookLoop.Services.Orders
{
	public interface IOrderService
	{
		// 預覽（供 AJAX 試算）
		Task<PricingResult> PreviewAsync(PricingInputs input);

		// 建立訂單（包含券與點數應用、快照、交易）
		Task<Result<int>> PlaceOrderAsync(PricingInputs input);
	}
}
