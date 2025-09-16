using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Ordersys.Models;


//EntityFrameworkCore.Tools -Version 9.0.8
namespace BookLoop.Ordersys.Controllers
{
	[Area("Orders")]
	public class OrdersController : Controller
    {
        private readonly OrdersysContext _context;

        public OrdersController(OrdersysContext context)
        {
            _context = context;
        }

		// GET: Orders
		public async Task<IActionResult> Index(int page = 1, int pageSize = 10, int? searchOrderID = null)
		{
			var query = _context.Orders
				.Include(o => o.Customer)
				.AsQueryable();

			// 搜尋條件
			if (searchOrderID.HasValue)
			{
				query = query.Where(o => o.OrderID == searchOrderID.Value);
			}

			// 總筆數
			var totalCount = await query.CountAsync();

			// 分頁資料
            var orders = await query
			   .OrderByDescending(o => o.OrderID) // ← 從第一筆開始
				.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

			// 分頁資訊丟到 ViewBag
			ViewBag.CurrentPage = page;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			ViewBag.TotalCount = totalCount;

			return View(orders);
		}
		// GET: Orders/Details/5
		public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "CustomerID");
            return View();
        }





		public async Task<IActionResult> OrderDetails(int id)
		{
			// id = OrderID
			var orderDetails = await _context.OrderDetails
				.Include(od => od.Order)    // 如果要帶 Order 的資訊
				.Where(od => od.OrderID == id)
				.ToListAsync();

			//if (orderDetails == null || !orderDetails.Any())
			//{
			//	return NotFound();
			//}

			return View(orderDetails);
		}







		// POST: Orders/Create
		// To protect from overposting attacks, enable the specific properties you want to bind to.
		// For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
		[HttpPost]
        [ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("OrderID,CustomerID,OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount")] Order order)
		{
            if (ModelState.IsValid)
            {
				order.CreatedAt = DateTime.UtcNow;
				_context.Add(order);
                await _context.SaveChangesAsync();
				TempData["Success"] = "訂單已建立";
				return RedirectToAction(nameof(Index));
			}
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "CustomerID", order.CustomerID);
            return View(order);
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "CustomerID", order.CustomerID);
            return View(order);
        }

        // POST: Orders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("OrderID,CustomerID,OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount")] Order order)
		{
            if (id != order.OrderID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.OrderID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
				TempData["Success"] = "訂單已更新";
				return RedirectToAction(nameof(Index));
			}
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "CustomerID", order.CustomerID);
            return View(order);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                _context.Orders.Remove(order);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderID == id);
        }
    }
}
