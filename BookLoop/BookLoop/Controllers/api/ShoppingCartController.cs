using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data;
using BookLoop.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers.api
{
	[ApiController]
	[Route("api/[controller]")]
	public class ShoppingCartController : ControllerBase
	{
		private readonly ShopDbContext _db;

		public ShoppingCartController(ShopDbContext db)
		{
			_db = db;
		}

		// ✅ 加入購物車
		[HttpPost("add")]
		public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
		{
			if (request.MemberID <= 0 || request.BookID <= 0)
				return BadRequest("會員ID或商品ID不正確");

			var cart = await _db.ShoppingCarts
				.Include(c => c.Items)
				.FirstOrDefaultAsync(c => c.MemberID == request.MemberID && c.IsActive);

			if (cart == null)
			{
				cart = new ShoppingCart
				{
					MemberID = request.MemberID,
					CreatedDate = DateTime.Now,
					IsActive = true
				};
				_db.ShoppingCarts.Add(cart);
				await _db.SaveChangesAsync();
			}

			var item = cart.Items.FirstOrDefault(i => i.BookID == request.BookID);
			if (item == null)
			{
				item = new ShoppingCartItems
				{
					CartID = cart.CartID,
					BookID = request.BookID,
					Quantity = request.Quantity,
					UnitPrice = request.UnitPrice
				};
				_db.ShoppingCartItems.Add(item);
			}
			else
			{
				item.Quantity += request.Quantity;
			}

			await _db.SaveChangesAsync();

			return Ok(new { success = true, message = "商品已加入購物車" });
		}

		// ✅ 查看購物車
		[HttpGet("{memberId:int}")]
		public async Task<IActionResult> GetCart(int memberId)
		{
			var cart = await _db.ShoppingCarts
				.Include(c => c.Items)
				.ThenInclude(i => i.Book)
				.FirstOrDefaultAsync(c => c.MemberID == memberId && c.IsActive);

			if (cart == null)
				return Ok(new { items = Array.Empty<object>() });

			var result = cart.Items.Select(i => new
			{
				itemId = i.ItemID,
				bookId = i.BookID,
				title = i.Book?.Title ?? "(已下架)",
				price = i.UnitPrice,
				quantity = i.Quantity,
				subtotal = i.UnitPrice * i.Quantity
			});

			return Ok(result);
		}

		// ✅ 移除購物車項目
		[HttpDelete("remove/{itemId:int}")]
		public async Task<IActionResult> RemoveItem(int itemId)
		{
			var item = await _db.ShoppingCartItems.FindAsync(itemId);
			if (item == null)
				return NotFound();

			_db.ShoppingCartItems.Remove(item);
			await _db.SaveChangesAsync();

			return Ok(new { success = true, message = "已移除商品" });
		}
	}

	public class AddToCartRequest
	{
		public int MemberID { get; set; }
		public int BookID { get; set; }
		public int Quantity { get; set; }
		public decimal UnitPrice { get; set; }
	}
}