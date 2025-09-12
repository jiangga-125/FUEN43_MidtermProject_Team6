using BookLoop.Ordersys.Models;
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
		public async Task<IActionResult> DetailsByOrder(int orderId)
		{
			var shipments = await _context.Shipments
										  .Where(s => s.OrderId == orderId)
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
                .FirstOrDefaultAsync(m => m.ShipmentId == id);
            if (shipment == null)
            {
                return NotFound();
            }

            return View(shipment);
        }

        // GET: Shipments/Create
        public IActionResult Create()
        {
            ViewData["AddressId"] = new SelectList(_context.OrderAddresses, "OrderAddressId", "OrderAddressId");
            ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId");
            return View();
        }

        // POST: Shipments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ShipmentId,OrderId,Provider,AddressId,TrackingNumber,ShippedDate,DeliveredDate,Status,CreatedAt,UpdatedAt")] Shipment shipment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shipment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AddressId"] = new SelectList(_context.OrderAddresses, "OrderAddressId", "OrderAddressId", shipment.AddressId);
            ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", shipment.OrderId);
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
            ViewData["AddressId"] = new SelectList(_context.OrderAddresses, "OrderAddressId", "OrderAddressId", shipment.AddressId);
            ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", shipment.OrderId);
            return View(shipment);
        }

        // POST: Shipments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ShipmentId,OrderId,Provider,AddressId,TrackingNumber,ShippedDate,DeliveredDate,Status,CreatedAt,UpdatedAt")] Shipment shipment)
        {
            if (id != shipment.ShipmentId)
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
                    if (!ShipmentExists(shipment.ShipmentId))
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
            ViewData["AddressId"] = new SelectList(_context.OrderAddresses, "OrderAddressId", "OrderAddressId", shipment.AddressId);
            ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", shipment.OrderId);
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
                .FirstOrDefaultAsync(m => m.ShipmentId == id);
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
            return _context.Shipments.Any(e => e.ShipmentId == id);
        }
    }
}
