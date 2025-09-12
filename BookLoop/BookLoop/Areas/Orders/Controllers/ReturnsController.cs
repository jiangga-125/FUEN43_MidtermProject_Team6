using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Ordersys.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Ordersys.Controllers
{
	[Area("Orders")]
	public class ReturnsController : Controller
	{
		private readonly OrdersysContext _context;

		public ReturnsController(OrdersysContext context)
		{
			_context = context;
		}

		// GET: Returns
		public async Task<IActionResult> Index(int page = 1, int pageSize = 10, int? searchReturnId = null)
		{
			var query = _context.Returns
				.Include(r => r.Order)
				.ThenInclude(o => o.Customer) // 如果畫面要顯示客戶資訊
				.AsQueryable();

			// 搜尋條件：依 ReturnId
			if (searchReturnId.HasValue)
			{
				query = query.Where(r => r.ReturnId == searchReturnId.Value);
			}

			// 總筆數
			var totalCount = await query.CountAsync();

			// 分頁資料
			var returns = await query
				.OrderByDescending(r => r.ReturnId)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			// 分頁資訊
			ViewBag.CurrentPage = page;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			ViewBag.TotalCount = totalCount;

			// 回填搜尋條件
			ViewBag.SearchReturnId = searchReturnId;

			return View(returns);
		}


		// GET: ReturnsDetails
		public async Task<IActionResult> ReturnsDetails(int id)
		{
			// 找到 Return 記錄
			var returnEntity = await _context.Returns
				.FirstOrDefaultAsync(r => r.ReturnId == id);

			if (returnEntity == null)
			{
				return NotFound();
			}
			// 找到這個退貨單對應的訂單明細
			var orderDetails = await _context.OrderDetails
				.Where(od => od.OrderId == returnEntity.OrderId)
				.ToListAsync();

			// 把退貨單資訊傳到 ViewBag，方便在 View 裡顯示
			ViewBag.ReturnId = returnEntity.ReturnId;
			ViewBag.OrderId = returnEntity.OrderId;
			ViewBag.Status = returnEntity.Status;

			return View(orderDetails); // 對應到 Views/Returns/ReturnsDetails.cshtml
		}








		// GET: Returns/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var ret = await _context.Returns
				.Include(r => r.Order)
				  .ThenInclude(o => o.Customer)// 包含 Customer 資料
				.FirstOrDefaultAsync(r => r.ReturnId == id);

			if (ret == null) return NotFound();

			return View(ret);
		}

		// GET: Returns/Create
		public IActionResult Create()
		{
			ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId");
			return View();
		}

		// POST: Returns/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("ReturnId,OrderId,ReturnReason,ReturnType,Status,ReturnedDate")] Return ret)
		{
			if (ModelState.IsValid)
			{
				_context.Add(ret);
				await _context.SaveChangesAsync();
				return RedirectToAction(nameof(Index));
			}
			ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", ret.OrderId);
			return View(ret);
		}

		// GET: Returns/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var ret = await _context.Returns.FindAsync(id);
			if (ret == null) return NotFound();

			ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", ret.OrderId);
			return View(ret);
		}

		// POST: Returns/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("ReturnId,OrderId,ReturnReason,ReturnType,Status,ReturnedDate")] Return ret)
		{
			if (id != ret.ReturnId) return NotFound();

			if (ModelState.IsValid)
			{
				try
				{
					_context.Update(ret);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!ReturnExists(ret.ReturnId)) return NotFound();
					else throw;
				}
				return RedirectToAction(nameof(Index));
			}

			ViewData["OrderId"] = new SelectList(_context.Orders, "OrderId", "OrderId", ret.OrderId);
			return View(ret);
		}

		// GET: Returns/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var ret = await _context.Returns
				.Include(r => r.Order)
				.FirstOrDefaultAsync(r => r.ReturnId == id);

			if (ret == null) return NotFound();

			return View(ret);
		}

		// POST: Returns/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var ret = await _context.Returns.FindAsync(id);
			if (ret != null)
			{
				_context.Returns.Remove(ret);
				await _context.SaveChangesAsync();
			}
			return RedirectToAction(nameof(Index));
		}

		private bool ReturnExists(int id)
		{
			return _context.Returns.Any(e => e.ReturnId == id);
		}
	}
}