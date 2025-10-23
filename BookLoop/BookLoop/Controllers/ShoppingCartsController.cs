using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers
{
	public class ShoppingCartsController : Controller
	{
		private readonly ShopDbContext _context;

		public ShoppingCartsController(ShopDbContext context)
		{
			_context = context;
		}

		// GET: ShoppingCarts
		public async Task<IActionResult> Index()
		{
			var carts = await _context.ShoppingCarts
				.Include(c => c.Member)          // 加上這行，讓 Member 導覽屬性被讀取
				.Include(c => c.Items)
				.ThenInclude(i => i.Book)
				.ToListAsync();

			// 取得所有書籍放進 ViewBag
			ViewBag.Books = await _context.Books.Where(b => !b.IsDeleted).ToListAsync();
			ViewBag.Members = await _context.Members.ToListAsync();  // 取得會員清單

			return View(carts);
		}

		// GET: ShoppingCarts/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var shoppingCart = await _context.ShoppingCarts
				.Include(c => c.Items)
				.ThenInclude(i => i.Book)
				.FirstOrDefaultAsync(m => m.CartID == id);

			if (shoppingCart == null) return NotFound();

			return View(shoppingCart);
		}

		

		// POST: ShoppingCarts/Create
		public async Task<IActionResult> Create()
		{
			// 取得所有會員放進下拉選單
			var members = await _context.Members.ToListAsync();
			ViewBag.Members = new SelectList(members, "MemberID", "Username");
			return View();
		}

		// POST: ShoppingCarts/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("CartID,CreatedDate,IsActive,UpdatedDate,MemberID")] ShoppingCart shoppingCart)
		{
			if (ModelState.IsValid)
			{
				shoppingCart.CreatedDate = DateTime.Now;
				shoppingCart.UpdatedDate = DateTime.Now;

				_context.Add(shoppingCart);
				await _context.SaveChangesAsync();
				return RedirectToAction(nameof(Index));
			}

			// 如果驗證失敗，要重新給下拉選單
			var members = await _context.Members.ToListAsync();
			ViewBag.Members = new SelectList(members, "MemberID", "Username", shoppingCart.MemberID);

			return View(shoppingCart);
		}

		// GET: ShoppingCarts/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var shoppingCart = await _context.ShoppingCarts.FindAsync(id);
			if (shoppingCart == null) return NotFound();

			return View(shoppingCart);
		}

		// POST: ShoppingCarts/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("CartID,CreatedDate,IsActive,UpdatedDate")] ShoppingCart shoppingCart)
		{
			if (id != shoppingCart.CartID) return NotFound();

			if (ModelState.IsValid)
			{
				try
				{
					_context.Update(shoppingCart);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!ShoppingCartExists(shoppingCart.CartID)) return NotFound();
					else throw;
				}
				return RedirectToAction(nameof(Index));
			}
			return View(shoppingCart);
		}

		// GET: ShoppingCarts/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var shoppingCart = await _context.ShoppingCarts
				.FirstOrDefaultAsync(m => m.CartID == id);

			if (shoppingCart == null) return NotFound();

			return View(shoppingCart);
		}

		// POST: ShoppingCarts/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var shoppingCart = await _context.ShoppingCarts
				.Include(c => c.Items) // 先把購物車裡的商品讀出來
				.FirstOrDefaultAsync(c => c.CartID == id);

			if (shoppingCart != null)
			{
				// 刪除購物車裡的商品
				_context.ShoppingCartItems.RemoveRange(shoppingCart.Items);

				// 刪除購物車
				_context.ShoppingCarts.Remove(shoppingCart);

				await _context.SaveChangesAsync();
			}

			return RedirectToAction(nameof(Index));
		}

		// POST: ShoppingCarts/AddToCart
		
		[HttpPost]
		public async Task<IActionResult> AddToCart(int memberId, int bookId, int quantity = 1)
		{
			// 找對應會員的購物車（如果不存在就創建）
			var cart = await _context.ShoppingCarts
				.Include(c => c.Items)
				.FirstOrDefaultAsync(c => c.MemberID == memberId && c.IsActive);

			if (cart == null)
			{
				cart = new ShoppingCart
				{
					MemberID = memberId,
					CreatedDate = DateTime.Now,
					UpdatedDate = DateTime.Now,
					IsActive = true
				};
				_context.ShoppingCarts.Add(cart);
				await _context.SaveChangesAsync();
			}

			var book = await _context.Books.FindAsync(bookId);
			if (book == null) return NotFound("找不到該書籍。");

			var existingItem = cart.Items.FirstOrDefault(i => i.BookID == bookId);
			if (existingItem != null)
			{
				existingItem.Quantity += quantity;
			}
			else
			{
				decimal price = (book.SalePrice.HasValue && book.SalePrice.Value > 0)
					? book.SalePrice.Value
					: book.ListPrice;

				cart.Items.Add(new ShoppingCartItems
				{
					BookID = bookId,
					Quantity = quantity,
					UnitPrice = price
				});
			}

			cart.UpdatedDate = DateTime.Now;
			await _context.SaveChangesAsync();

			return RedirectToAction(nameof(Index));
		}

		// 顯示購物車內容
		public async Task<IActionResult> ViewCart(int id)
		{
			var cart = await _context.ShoppingCarts
				.Include(c => c.Items)
				.ThenInclude(i => i.Book)
				.FirstOrDefaultAsync(c => c.CartID == id);

			if (cart == null) return NotFound();

			return View(cart);
		}

		// 移除單筆商品
		[HttpPost]
		public async Task<IActionResult> RemoveItem(int itemId)
		{
			var item = await _context.ShoppingCartItems.FindAsync(itemId);
			if (item == null) return NotFound();

			_context.ShoppingCartItems.Remove(item);
			await _context.SaveChangesAsync();

			return RedirectToAction(nameof(ViewCart), new { id = item.CartID });
		}

		private bool ShoppingCartExists(int id)
		{
			return _context.ShoppingCarts.Any(e => e.CartID == id);
		}
	}
}
