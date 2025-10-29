using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using BookLoop.Data;    // 若你的 Data namespace 不同請改
using BookLoop.Models;  // 若你的 Models namespace 不同請改

namespace BookLoop.Controllers
{
	[Route("api/dashboard")]
	[ApiController]
	public class DashboardController : ControllerBase
	{
		private readonly ShopDbContext _shop;         // 訂單/商店資料（Orders）
		private readonly BookSystemContext _bookSys;  // 書籍/庫存資料（BookInventory）

		public DashboardController(ShopDbContext shop, BookSystemContext bookSys)
		{
			_shop = shop;
			_bookSys = bookSys;
		}

		[HttpGet("summary")]
		public async Task<IActionResult> Summary()
		{
			var today = DateTime.Today;
			var tomorrow = today.AddDays(1);

			// 今日訂單數（所有狀態）
			var todayOrderCount = await _shop.Set<Order>()
				.Where(o => o.OrderDate >= today && o.OrderDate < tomorrow)
				.CountAsync();

			// 今日營收（排除已取消 Status == 4）
			var todayRevenue = await _shop.Set<Order>()
				.Where(o => o.OrderDate >= today && o.OrderDate < tomorrow && o.Status != (byte)4)
				.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

			// 未出貨（定義為 Status == 0 或 1）
			var unshippedCount = await _shop.Set<Order>()
				.Where(o => o.Status == (byte)0 || o.Status == (byte)1)
				.CountAsync();

			// 先把最新的原始欄位拉回記憶體（最多 5 筆）
			var latestOrdersRaw = await _shop.Set<Order>()
				.OrderByDescending(o => o.CreatedAt)
				.Take(5)
				.Select(o => new {
					o.OrderID,
					o.MemberID,
					o.TotalAmount,
					o.Status,
					o.CreatedAt
				})
				.ToListAsync();

			// 在記憶體中做 mapping（StatusText）- method B（你選的方式）
			var latestOrders = latestOrdersRaw.Select(o => new {
				Id = o.OrderID,
				MemberId = o.MemberID,
				TotalAmount = o.TotalAmount,
				Status = o.Status,
				StatusText = o.Status == 0 ? "待付款"
						   : o.Status == 1 ? "已下訂"
						   : o.Status == 2 ? "已出貨"
						   : o.Status == 3 ? "完成訂單"
						   : o.Status == 4 ? "已取消"
						   : "未知狀態",
				CreatedAt = o.CreatedAt
			}).ToList();

			// 若沒有資料，latestOrders 會是空 list（前端會處理友善提示）
			// 庫存告警（BookInventory）
			int inventoryAlerts = 0;
			try
			{
				inventoryAlerts = await _bookSys.Set<BookInventory>()
					.Where(b => (b.OnHand - b.Reserved) <= 5)
					.CountAsync();
			}
			catch
			{
				// 若 BookInventory 不在此 context 或查詢失敗，回 0（不讓 API 失敗）
				inventoryAlerts = 0;
			}

			return Ok(new
			{
				todayOrderCount,
				todayRevenue,
				unshippedCount,
				inventoryAlerts,
				latestOrders
			});
		}

		[HttpGet("sales14")]
		public async Task<IActionResult> Sales14()
		{
			var end = DateTime.Today;
			var start = end.AddDays(-13);

			var sales = await _shop.Set<Order>()
				.Where(o => o.OrderDate >= start && o.OrderDate < end.AddDays(1) && o.Status != (byte)4)
				.GroupBy(o => o.OrderDate.Date)
				.Select(g => new { Date = g.Key, Sum = g.Sum(x => (decimal?)x.TotalAmount) ?? 0m })
				.ToListAsync();

			var labels = Enumerable.Range(0, 14).Select(i => start.AddDays(i).ToString("MM/dd")).ToArray();
			var data = Enumerable.Range(0, 14).Select(i => {
				var d = start.AddDays(i).Date;
				var row = sales.FirstOrDefault(s => s.Date == d);
				return (double)(row?.Sum ?? 0m);
			}).ToArray();

			return Ok(new { labels, data });
		}
	}
}
