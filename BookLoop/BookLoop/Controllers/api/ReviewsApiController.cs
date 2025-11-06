using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using BookLoop.Data;
using BookLoop.Models;

namespace BookLoop.Controllers.api
{
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[ApiController]
	[Route("api/[controller]/[action]")]
	public class ReviewsApiController : ControllerBase
	{
		private readonly OrdersysContext _ordersDb;
		private readonly MemberContext _memberDb;

		public ReviewsApiController(OrdersysContext ordersDb, MemberContext memberDb)
		{
			_ordersDb = ordersDb;
			_memberDb = memberDb;
		}

		// ✅ 查詢會員已完成訂單的書籍
		[HttpGet("{memberId}")]
		public async Task<IActionResult> GetPurchasedBooks(int memberId)
		{
			const int CompletedStatus = 1; // 1=已完成

			// 🔍 確認會員存在
			var exists = await _memberDb.Members.AnyAsync(m => m.MemberID == memberId);
			if (!exists)
				return NotFound(new { message = $"找不到會員 ID: {memberId}" });

			// ✅ 查詢該會員所有已完成訂單的書籍
			var books = await (
				from o in _ordersDb.Orders
				join od in _ordersDb.OrderDetails on o.OrderID equals od.OrderID
				join b in _ordersDb.Books on od.BookID equals b.BookID
				where o.MemberID == memberId && o.Status == CompletedStatus
				select new { b.BookID, b.Title }
			).Distinct().ToListAsync();

			if (!books.Any())
				return Ok(new { message = "尚未有可評論的書籍", data = new List<object>() });

			return Ok(books);
		}
	}
}
