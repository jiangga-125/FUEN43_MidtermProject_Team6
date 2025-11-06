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
	public class ReturnController : ControllerBase
	{
		private readonly OrdersysContext _context;

		public ReturnController(OrdersysContext context)
		{
			_context = context;
		}

		// 1️⃣ 申請退貨
		[HttpPost("create")]
		public async Task<IActionResult> CreateReturn([FromBody] Return ret)
		{
			if (ret == null || ret.OrderID <= 0 || string.IsNullOrEmpty(ret.ReturnReason))
				return BadRequest("退貨資料不完整");

			ret.Status = 0; // 初始狀態：申請中
			ret.ReturnedDate = null;
			_context.Returns.Add(ret);
			await _context.SaveChangesAsync();

			return Ok(new { ret.ReturnID, message = "退貨申請成功" });
		}

		// 2️⃣ 取得單筆訂單的所有退貨紀錄
		[HttpGet("order/{orderId}")]
		public async Task<IActionResult> GetReturnsByOrder(int orderId)
		{
			var returns = await _context.Returns
				.Where(r => r.OrderID == orderId)
				.OrderByDescending(r => r.ReturnID)
				.ToListAsync();

			return Ok(returns);
		}

		// 3️⃣ 取消退貨
		[HttpPost("cancel/{returnId}")]
		public async Task<IActionResult> CancelReturn(int returnId)
		{
			var ret = await _context.Returns.FindAsync(returnId);
			if (ret == null)
				return NotFound("找不到退貨紀錄");

			ret.Status = 9; // 自訂 9 代表取消
			await _context.SaveChangesAsync();

			return Ok(new { message = "退貨已取消" });
		}

		// 4️⃣ 取得單筆退貨明細（可選）
		[HttpGet("{returnId}")]
		public async Task<IActionResult> GetReturnDetail(int returnId)
		{
			var ret = await _context.Returns
				.Include(r => r.Order)
				.ThenInclude(o => o.OrderDetails)
				.ThenInclude(od => od.Book)
				.FirstOrDefaultAsync(r => r.ReturnID == returnId);

			if (ret == null)
				return NotFound("找不到退貨紀錄");

			return Ok(ret);
		}
	}
}
