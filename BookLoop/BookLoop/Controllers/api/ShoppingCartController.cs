using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BookLoop.Controllers.api
{
	[ApiController]
	[Route("api/[controller]")]
	public class ShoppingCartController : ControllerBase
	{
		private readonly ShopDbContext _shopDbContext;
		private readonly MemberContext _memberContext;

		public ShoppingCartController(ShopDbContext shopDbContext, MemberContext memberContext)
		{
			_shopDbContext = shopDbContext;   // ✅ 用來操作購物車、書籍、訂單
			_memberContext = memberContext;   // ✅ 用來操作會員資料
		}

		// ✅ 加入購物車
		[HttpPost("add")]
		[AllowAnonymous]
		public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
		{
			try
			{
				// --- 改動 1: 用 MemberContext 查會員 ---
				var member = await _memberContext.Members.FindAsync(request.MemberID);
				if (member == null)
					return BadRequest("會員不存在");

				// --- 改動 2: 用 ShopDbContext 查書籍 ---
				var book = await _shopDbContext.Books.FindAsync(request.BookID);
				if (book == null)
					return BadRequest("書籍不存在");

				// --- 改動 3: 用 ShopDbContext 查購物車 ---
				var cart = await _shopDbContext.ShoppingCarts
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
					_shopDbContext.ShoppingCarts.Add(cart);
					await _shopDbContext.SaveChangesAsync();
				}

				var item = cart.Items.FirstOrDefault(i => i.BookID == request.BookID);
				if (item == null)
				{
					item = new ShoppingCartItems
					{
						CartID = cart.CartID,
						BookID = request.BookID,
						Quantity = request.Quantity,
						UnitPrice = request.UnitPrice,
						Cart = cart,
						Book = book
					};
					_shopDbContext.ShoppingCartItems.Add(item);
				}
				else
				{
					item.Quantity += request.Quantity;
				}

				await _shopDbContext.SaveChangesAsync();

				return Ok(new { success = true, message = "商品已加入購物車" });
			}
			catch (Exception ex)
			{
				// 這裡捕捉所有例外並回傳完整訊息
				// 注意：正式環境可以記 log，但不要直接把 ex.Message 回前端以免洩漏資訊
				return StatusCode(500, new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
			}
		}
			// ✅ 查看購物車
			[HttpGet("{memberId:int}")]
			[AllowAnonymous]
		public async Task<IActionResult> GetCart(int memberId)
		{
			// --- 改動 5: 用 ShopDbContext 查購物車 ---
			var cart = await _shopDbContext.ShoppingCarts
				.Include(c => c.Items)
				.ThenInclude(i => i.Book)
				.FirstOrDefaultAsync(c => c.MemberID == memberId && c.IsActive);

			Console.WriteLine($"🛒 AddToCart called: memberId={memberId}");

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
			// --- 改動 6: 用 ShopDbContext 查購物車項目 ---
			var item = await _shopDbContext.ShoppingCartItems.FindAsync(itemId);
			if (item == null)
				return NotFound();

			_shopDbContext.ShoppingCartItems.Remove(item);
			await _shopDbContext.SaveChangesAsync();

			return Ok(new { success = true, message = "已移除商品" });
		}

		// 測試 DbContext 有哪些 Entity
		[HttpGet("debug/entities")]
		[AllowAnonymous]   // 先不用登入，方便測試
		public IActionResult DebugEntities()
		{
			var entities = _shopDbContext.Model.GetEntityTypes()
				.Select(e => e.Name)
				.ToList();

			return Ok(entities);
		}
		[HttpGet("test")]
		[AllowAnonymous]  // 先不要授權
		public async Task<IActionResult> TestCart()
		{
			var carts = await _shopDbContext.ShoppingCarts
							.Include(c => c.Items)
							.ThenInclude(i => i.Book)
							.ToListAsync();

			return Ok(carts); // 直接回傳 JSON
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
