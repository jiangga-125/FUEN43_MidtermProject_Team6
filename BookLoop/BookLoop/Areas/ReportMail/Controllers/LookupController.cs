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

		[HttpPost]
		public async Task<IActionResult> MaxRank([FromBody] MaxRankRequest req)
		{
			// 解析前端送來的「選到哪些類別」
			var picked = new List<int>();
			var fCats = req?.Filters?.FirstOrDefault(f => f.FieldName == "CategoryID");
			if (fCats != null && !string.IsNullOrWhiteSpace(fCats.ValueJson))
			{
				try
				{
					using var doc = System.Text.Json.JsonDocument.Parse(fCats.ValueJson);
					if (doc.RootElement.TryGetProperty("values", out var arr))
					{
						foreach (var x in arr.EnumerateArray())
							if (x.TryGetInt32(out var v)) picked.Add(v);
					}
				}
				catch { /* 忽略解析錯誤，走預設 */ }
			}

			if (picked.Count > 0)
				return Json(new { maxRank = picked.Count });

			// 未選任何類別 → 上限 = 全部類別數
			var totalCategories = await _shop.Categories.AsNoTracking().CountAsync();
			return Json(new { maxRank = totalCategories });
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

	public class MaxRankRequest
	{
		public string BaseKind { get; set; } = "sales";  // "sales" | "borrow"
		public List<FilterDto> Filters { get; set; } = new();
	}
	public class FilterDto
	{
		public string FieldName { get; set; }
		public string Operator { get; set; }
		public string ValueJson { get; set; }
	}

}