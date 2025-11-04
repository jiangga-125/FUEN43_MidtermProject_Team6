using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers.Api
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize] // 需要登入
	public class OrderController : ControllerBase
	{
		private readonly ShopDbContext _db;

		public OrderController(ShopDbContext db)
		{
			_db = db;
		}

		// 1️⃣ 建立訂單
		[HttpPost("create")]
		public async Task<IActionResult> CreateOrder([FromBody] Order order)
		{
			if (order == null || order.OrderDetails == null || !order.OrderDetails.Any())
				return BadRequest("訂單資料不完整");

			order.CreatedAt = DateTime.UtcNow;
			_db.Orders.Add(order);
			await _db.SaveChangesAsync();

			return Ok(new { order.OrderID });
		}

		// 2️⃣ 取得會員的所有訂單（排除已刪除）
		[HttpGet("member/{memberId}")]
		public async Task<IActionResult> GetOrdersByMember(int memberId)
		{
			var orders = await _db.Orders
				.Where(o => o.MemberID == memberId && o.Status != 9) // 排除軟刪除
				.Include(o => o.OrderDetails)
				.ThenInclude(od => od.Book)
				.OrderByDescending(o => o.CreatedAt)
				.ToListAsync();

			return Ok(orders);
		}

		// 3️⃣ 取得單筆訂單明細（包含已刪除也能看到）
		[HttpGet("{orderId}")]
		public async Task<IActionResult> GetOrderDetail(int orderId)
		{
			var order = await _db.Orders
				.Include(o => o.OrderDetails)
				.ThenInclude(od => od.Book)
				.FirstOrDefaultAsync(o => o.OrderID == orderId);

			if (order == null)
				return NotFound("找不到訂單");

			return Ok(order);
		}

		// 4️⃣ 取消訂單（Status = 0）
		[HttpPost("cancel/{orderId}")]
		public async Task<IActionResult> CancelOrder(int orderId)
		{
			var order = await _db.Orders.FindAsync(orderId);
			if (order == null)
				return NotFound("找不到訂單");

			order.Status = 0;
			await _db.SaveChangesAsync();

			return Ok(new { message = "訂單已取消" });
		}

		// 5️⃣ 軟刪除訂單（Status = 9）
		[HttpPost("delete/{orderId}")]
		public async Task<IActionResult> DeleteOrder(int orderId)
		{
			var order = await _db.Orders.FindAsync(orderId);
			if (order == null)
				return NotFound("找不到訂單");

			order.Status = 9; // 標記為已刪除
			await _db.SaveChangesAsync();

			return Ok(new { message = "訂單已軟刪除" });
		}
	}
}