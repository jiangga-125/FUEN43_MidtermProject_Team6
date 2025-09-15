using Microsoft.AspNetCore.Mvc;
using 會員.Services.Orders;
using 會員.Services.Pricing;
using 會員.ViewModels;

[Route("[controller]/[action]")]
public class CheckoutController : Controller
{
	private readonly IOrderService _order;

	public CheckoutController(IOrderService order) => _order = order;

	// GET /Checkout/Index
	[HttpGet]
	public IActionResult Index()
	{
		// 顯示 Razor 頁面（輸入欄 + 預覽 + 下單）
		return View();
	}

	// POST /Checkout/Preview  (AJAX 試算)
	[HttpPost]
	public async Task<IActionResult> Preview([FromBody] CheckoutPreviewVm vm)
	{
		// 將 ViewModel 轉為定價引擎輸入
		var input = new PricingInputs
		{
			MemberId = vm.MemberId,
			Subtotal = vm.Subtotal,
			CouponCode = vm.CouponCode,
			UsePoints = vm.UsePoints
		};

		var result = await _order.PreviewAsync(input);
		return Json(result); // 回傳 JSON 給前端
	}

	// POST /Checkout/PlaceOrder  (表單/或 AJAX 下單)
	[HttpPost]
	[ValidateAntiForgeryToken] // 需要 Anti-forgery Token
	public async Task<IActionResult> PlaceOrder(CheckoutPlaceOrderVm vm)
	{
		var input = new PricingInputs
		{
			MemberId = vm.MemberId,
			Subtotal = vm.Subtotal,
			CouponCode = vm.CouponCode,
			UsePoints = vm.UsePoints
		};

		var res = await _order.PlaceOrderAsync(input);

		// 如果是 AJAX 請求，直接回 JSON，讓前端顯示成功/失敗提示
	
			return Json(res);

		// 非 AJAX：用文字或導頁
		if (!res.Ok) return Content($"下單失敗：{res.Message}");
		return Content($"下單成功，OrderID={res.Data}");
		// 之後你也可以改成：return RedirectToAction("Detail", "Orders", new { id = res.Data });
	}
}
