using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Ordersys.Controllers
{
	[Area("Orders")]
	public class RentalsController : Controller
    {
        private readonly OrdersysContext _context;

        public RentalsController(OrdersysContext context)
        {
            _context = context;
        }



		// GET: Rentals/ByID/5  顯示指定訂單的所有租借品

		public async Task<IActionResult> ByID(int orderID)
		{
			var rentals = await _context.Rentals
										.Where(r => r.OrderID == orderID)
										.Include(r => r.Order)
										.ToListAsync();

			ViewData["OrderID"] = orderID; // 傳給 View，讓新增時知道是哪張訂單
			return View(rentals);
		}


		// GET: Rentals/CreateByID/5 
		public IActionResult CreateByID(int orderID)
		{
			var rental = new Rental
			{
				OrderID = orderID,
				RentalStart = DateTime.Now,
				RentalEnd = DateTime.Now.AddDays(7) // 預設一週，可自行調整
			};

			return View(rental);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateByID(Rental rental)
		{
			if (ModelState.IsValid)
			{
				_context.Add(rental);
				await _context.SaveChangesAsync();
				// ⬇⬇ 改成 ByID，保持一致
				return RedirectToAction(nameof(ByID), new { orderID = rental.OrderID });
			}
			return View(rental);
		}
		public async Task<IActionResult> EditByID(int id)
		{
			var rental = await _context.Rentals.FindAsync(id);
			if (rental == null) return NotFound();
			return View(rental);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditByID(int id, Rental rental)
		{
			if (id != rental.RentalID) return NotFound();
			if (ModelState.IsValid)
			{
				try
				{
					_context.Update(rental);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!_context.Rentals.Any(e => e.RentalID == rental.RentalID)) return NotFound();
					else throw;
				}
				return RedirectToAction(nameof(ByID), new { orderID = rental.OrderID });
			}
			return View(rental);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteByID(int id)
		{
			var rental = await _context.Rentals.FindAsync(id);
			if (rental == null) return NotFound();

			int orderID = rental.OrderID;
			_context.Rentals.Remove(rental);
			await _context.SaveChangesAsync();

			return RedirectToAction(nameof(ByID), new { orderID });
		}










		// GET: Rentals
		public async Task<IActionResult> Index()
        {
            var ordersysContext = _context.Rentals.Include(r => r.Order);
            return View(await ordersysContext.ToListAsync());
        }

        // GET: Rentals/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rental = await _context.Rentals
                .Include(r => r.Order)
                .FirstOrDefaultAsync(m => m.RentalID == id);
            if (rental == null)
            {
                return NotFound();
            }

            return View(rental);
        }

        // GET: Rentals/Create
        public IActionResult Create()
        {
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID");
            return View();
        }

        // POST: Rentals/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RentalID,OrderID,ItemName,RentalStart,RentalEnd,ReturnedDate,Status")] Rental rental)
        {
            if (ModelState.IsValid)
            {
                _context.Add(rental);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", rental.OrderID);
            return View(rental);
        }

        // GET: Rentals/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rental = await _context.Rentals.FindAsync(id);
            if (rental == null)
            {
                return NotFound();
            }
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", rental.OrderID);
            return View(rental);
        }

        // POST: Rentals/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RentalID,OrderID,ItemName,RentalStart,RentalEnd,ReturnedDate,Status")] Rental rental)
        {
            if (id != rental.RentalID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rental);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RentalExists(rental.RentalID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", rental.OrderID);
            return View(rental);
        }

        // GET: Rentals/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rental = await _context.Rentals
                .Include(r => r.Order)
                .FirstOrDefaultAsync(m => m.RentalID == id);
            if (rental == null)
            {
                return NotFound();
            }

            return View(rental);
        }

        // POST: Rentals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rental = await _context.Rentals.FindAsync(id);
            if (rental != null)
            {
                _context.Rentals.Remove(rental);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RentalExists(int id)
        {
            return _context.Rentals.Any(e => e.RentalID == id);
        }
    }
}
