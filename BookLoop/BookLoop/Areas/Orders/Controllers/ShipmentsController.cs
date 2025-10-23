using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Ordersys.Controllers
{
	[Area("Orders")]
	public class ShipmentsController : Controller
    {
        private readonly OrdersysContext _context;

        public ShipmentsController(OrdersysContext context)
        {
            _context = context;
        }



		// AJAX 載入指定訂單的物流資訊
		public async Task<IActionResult> DetailsByOrder(int orderID)
		{
			var shipments = await _context.Shipments
										  .Where(s => s.OrderID == orderID)
										  .Include(s => s.Address) // 利用 Address 導覽屬性
										  .ToListAsync();

			// 回傳 PartialView，裡面用 <tr> 迴圈顯示物流
			return PartialView("_ShipmentPartial", shipments);
		}






		// GET: Shipments
		public async Task<IActionResult> Index()
        {
            var ordersysContext = _context.Shipments.Include(s => s.Address).Include(s => s.Order);
            return View(await ordersysContext.ToListAsync());
        }

        // GET: Shipments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shipment = await _context.Shipments
                .Include(s => s.Address)
                .Include(s => s.Order)
                .FirstOrDefaultAsync(m => m.ShipmentID == id);
            if (shipment == null)
            {
                return NotFound();
            }

            return View(shipment);
        }

        // GET: Shipments/Create
        public IActionResult Create()
        {
            ViewData["AddressID"] = new SelectList(_context.OrderAddresses, "OrderAddressID", "OrderAddressID");
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID");
            return View();
        }

        // POST: Shipments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ShipmentID,OrderID,Provider,AddressID,TrackingNumber,ShippedDate,DeliveredDate,Status,CreatedAt,UpdatedAt")] Shipment shipment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shipment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AddressID"] = new SelectList(_context.OrderAddresses, "OrderAddressID", "OrderAddressID", shipment.AddressID);
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", shipment.OrderID);
            return View(shipment);
        }

        // GET: Shipments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }
            ViewData["AddressID"] = new SelectList(_context.OrderAddresses, "OrderAddressID", "OrderAddressID", shipment.AddressID);
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", shipment.OrderID);
            return View(shipment);
        }

        // POST: Shipments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkID=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ShipmentID,OrderID,Provider,AddressID,TrackingNumber,ShippedDate,DeliveredDate,Status,CreatedAt,UpdatedAt")] Shipment shipment)
        {
            if (id != shipment.ShipmentID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shipment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShipmentExists(shipment.ShipmentID))
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
            ViewData["AddressID"] = new SelectList(_context.OrderAddresses, "OrderAddressID", "OrderAddressID", shipment.AddressID);
            ViewData["OrderID"] = new SelectList(_context.Orders, "OrderID", "OrderID", shipment.OrderID);
            return View(shipment);
        }

        // GET: Shipments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shipment = await _context.Shipments
                .Include(s => s.Address)
                .Include(s => s.Order)
                .FirstOrDefaultAsync(m => m.ShipmentID == id);
            if (shipment == null)
            {
                return NotFound();
            }

            return View(shipment);
        }

        // POST: Shipments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment != null)
            {
                _context.Shipments.Remove(shipment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ShipmentExists(int id)
        {
            return _context.Shipments.Any(e => e.ShipmentID == id);
        }
    }
}
