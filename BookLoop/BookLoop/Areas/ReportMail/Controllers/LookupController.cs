using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Shop;

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class LookupController : Controller
	{
		private readonly ShopDbContext _shop;
		public LookupController(ShopDbContext shop) => _shop = shop;

		[HttpGet]
		public async Task<IActionResult> Categories()
		{
			var list = await _shop.Categories
				.AsNoTracking()
				.OrderBy(x => x.CategoryName)
				.Select(x => new { value = x.CategoryID, text = x.CategoryName })
				.ToListAsync();
			return Json(list);
		}

		//[HttpGet]
		//public async Task<IActionResult> OrderStatuses()
		//{
		//	// 你的 Orders.Status 是 tinyint；這裡先給常見對照，找不到時顯示「狀態{code}」
		//	var map = new Dictionary<int, string> {
		//		{ 0, "新訂單" }, { 1, "已付款" }, { 2, "已出貨" }, { 3, "已完成" }, { 4, "已取消" }
		//	};

		//	var codes = await _shop.Orders
		//		.AsNoTracking()
		//		.Select(x => x.Status)
		//		.Distinct()
		//		.OrderBy(x => x)
		//		.ToListAsync();

		//	var list = codes.Select(c => new {
		//		value = (int)c,
		//		text = map.TryGetValue((int)c, out var name) ? name : $"狀態{c}"
		//	});
		//	return Json(list);
		//}
	}
}