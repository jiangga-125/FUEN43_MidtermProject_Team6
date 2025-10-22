using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace Ordersys.Controllers
{
	[Area("Orders")]
	public class OrderDetailsController : Controller
	{
		private readonly OrdersysContext _context;

		public OrderDetailsController(OrdersysContext context)
		{
			_context = context;
		}

		// GET: OrderDetails
		public async Task<IActionResult> Index(string searchProductName)
		{
			var query = _context.OrderDetails
								.Include(od => od.Book)
								.Include(od => od.Order)
								.AsQueryable();

			if (!string.IsNullOrEmpty(searchProductName))
			{
				query = query.Where(od => od.ProductName.Contains(searchProductName));
			}

			var orderDetails = await query
								.OrderByDescending(od => od.OrderDetailID)
								.ToListAsync();

			ViewBag.SearchProductName = searchProductName;
			return View(orderDetails);
		}

		// GET: OrderDetails/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var orderDetail = await _context.OrderDetails
									.Include(od => od.Book)
									.Include(od => od.Order)
									.FirstOrDefaultAsync(od => od.OrderDetailID == id);
			if (orderDetail == null) return NotFound();

			return View(orderDetail);
		}
		// GET: OrderDetails/Create
		public IActionResult Create()
		{
			// 下拉選單顯示書名，但值為 BookID
			ViewData["BookID"] = new SelectList(_context.Books, "BookID", "Title");
			ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID");

			// 將書籍資料傳給前端 JS 用於自動帶入商品名與單價
			ViewBag.BooksData = _context.Books
										.Select(b => new { b.BookID, b.Title, b.SalePrice })
										.ToList();

			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("OrderID,BookID,Quantity,ProductDiscountAmount")] OrderDetail orderDetail)
		{
			if (ModelState.IsValid)
			{
				// 確保選擇的書籍存在
				var book = await _context.Books.FindAsync(orderDetail.BookID);
				if (book == null)
				{
					ModelState.AddModelError("BookID", "選擇的書籍不存在");
				}
				else
				{
					// 自動帶入非 NULL 欄位
					orderDetail.ProductName = book.Title;
					orderDetail.UnitPrice = book.SalePrice ?? 0m;
					orderDetail.CreatedAt = DateTime.Now;

					_context.Add(orderDetail);
					await _context.SaveChangesAsync();
					return RedirectToAction(nameof(Index));
				}
			}

			// 驗證失敗或書籍不存在時，重新載入下拉選單
			ViewData["BookID"] = new SelectList(_context.Books, "BookID", "Title", orderDetail.BookID);
			ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", orderDetail.OrderID);
			ViewBag.BooksData = _context.Books.Select(b => new { b.BookID, b.Title, b.SalePrice }).ToList();
			return View(orderDetail);
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("OrderDetailID,OrderID,BookID,Quantity,ProductDiscountAmount")] OrderDetail orderDetail)
		{
			if (id != orderDetail.OrderDetailID) return NotFound();

			if (ModelState.IsValid)
			{
				try
				{
					var book = await _context.Books.FindAsync(orderDetail.BookID);
					if (book != null)
					{
						orderDetail.ProductName = book.Title;
						orderDetail.UnitPrice = book.SalePrice ?? 0m;
					}

					_context.Update(orderDetail);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!_context.OrderDetails.Any(e => e.OrderDetailID == id)) return NotFound();
					throw;
				}
				return RedirectToAction(nameof(Index));
			}

			ViewData["BookID"] = new SelectList(_context.Books, "BookID", "Title", orderDetail.BookID);
			ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", orderDetail.OrderID);
			return View(orderDetail);
		}

		// GET: OrderDetails/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var orderDetail = await _context.OrderDetails
									.Include(od => od.Book)
									.Include(od => od.Order)
									.FirstOrDefaultAsync(od => od.OrderDetailID == id);
			if (orderDetail == null) return NotFound();

			return View(orderDetail);
		}

		// POST: OrderDetails/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var orderDetail = await _context.OrderDetails.FindAsync(id);
			if (orderDetail != null)
			{
				_context.OrderDetails.Remove(orderDetail);
				await _context.SaveChangesAsync();
			}
			return RedirectToAction(nameof(Index));
		}
	}
}