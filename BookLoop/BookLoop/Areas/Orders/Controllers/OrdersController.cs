using BookLoop.Helpers;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Threading.Tasks;

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
		public async Task<IActionResult> Index(int page = 1,
			int pageSize = 10,
			int? searchOrderID = null,
			string? searchMemberName = null,
			int? searchMemberID = null,
			DateTime? searchStartDate = null,
			DateTime? searchEndDate = null,
			string? searchBookName = null)
		{
			var query = _context.Orders
				.Include(o => o.Member)       // 改用 Member
				.Include(o => o.OrderDetails)
				 .Include(o => o.Customer)
				.AsQueryable();

			// 搜尋條件
			if (searchOrderID.HasValue)
				query = query.Where(o => o.OrderID == searchOrderID.Value);

			if (!string.IsNullOrEmpty(searchMemberName))
				query = query.Where(o => o.Member != null && o.Member.Username.Contains(searchMemberName));

			if (searchMemberID.HasValue)
				query = query.Where(o => o.MemberID == searchMemberID.Value);

			if (searchStartDate.HasValue)
				query = query.Where(o => o.OrderDate >= searchStartDate.Value);

			if (searchEndDate.HasValue)
				query = query.Where(o => o.OrderDate <= searchEndDate.Value);

			if (!string.IsNullOrEmpty(searchBookName))
			{
				query = query.Where(o => o.OrderDetails.Any(od => od.ProductName.Contains(searchBookName)));
			}

			var totalCount = await query.CountAsync();

			var orders = await query
				.OrderByDescending(o => o.OrderID)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			foreach (var order in orders)
			{
				order.TotalAmount = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);
			}

			ViewBag.CurrentPage = page;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			ViewBag.TotalCount = totalCount;

			return View(orders);
		}

		// GET: Orders/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var order = await _context.Orders
				.Include(o => o.Member)
				.Include(o => o.OrderDetails)
				.FirstOrDefaultAsync(m => m.OrderID == id);

			if (order == null) return NotFound();

			return View(order);
		}

		// GET: Orders/Create
		public IActionResult Create()
		{
			ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username");
			var model = new Order
			{
				TotalAmount = 0 // 預設總金額
			};
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("OrderID,MemberID,OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount")] Order order)
		{
			if (ModelState.IsValid)
			{
				order.TotalAmount = order.OrderDetails?.Sum(od => od.UnitPrice * od.Quantity) ?? 0;

				order.CreatedAt = DateTime.UtcNow;
				_context.Add(order);
				await _context.SaveChangesAsync();
				TempData["Success"] = "訂單已建立";
				return RedirectToAction(nameof(Index));
			}


			ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", order.MemberID);
			return View(order);
		}

		// GET: Orders/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var order = await _context.Orders.FindAsync(id);
			if (order == null) return NotFound();

			ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", order.MemberID);
			return View(order);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount,MemberID")] Order orderInput)
		{
			if (!ModelState.IsValid)
			{
				ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", orderInput.MemberID);
				return View(orderInput);
			}

			var order = await _context.Orders.FindAsync(id);
			if (order == null) return NotFound();

			// 更新欄位，不覆蓋整個實體
			order.MemberID = orderInput.MemberID;
			order.OrderDate = orderInput.OrderDate;
			order.TotalAmount = orderInput.TotalAmount;
			order.Status = orderInput.Status;
			order.DiscountAmount = orderInput.DiscountAmount;
			order.DiscountCode = orderInput.DiscountCode;
			order.MemberCouponID = orderInput.MemberCouponID;
			order.CouponTypeSnap = orderInput.CouponTypeSnap;
			order.CouponValueSnap = orderInput.CouponValueSnap;
			order.CouponNameSnap = orderInput.CouponNameSnap;
			order.CouponDiscountAmount = orderInput.CouponDiscountAmount;

			try
			{
				await _context.SaveChangesAsync();
				TempData["Success"] = "訂單已更新";
				return RedirectToAction(nameof(Index));
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!OrderExists(id)) return NotFound();
				throw;
			}
		}
		// GET: Orders/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var order = await _context.Orders
				.Include(o => o.Member)
				.FirstOrDefaultAsync(m => m.OrderID == id);

			if (order == null) return NotFound();

			return View(order);
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var order = await _context.Orders.FindAsync(id);
			if (order != null)
			{
				_context.Orders.Remove(order);
				await _context.SaveChangesAsync();
			}
			return RedirectToAction(nameof(Index));
		}

		private bool OrderExists(int id)
		{
			return _context.Orders.Any(e => e.OrderID == id);
		}

		// GET: Orders/OrderDetails/5
		public async Task<IActionResult> OrderDetails(int id)
		{
			var orderDetails = await _context.OrderDetails
				.Where(od => od.OrderID == id)
				.Include(od => od.Book)
				.ToListAsync();

			// 即使沒有明細，也不回傳 NotFound
			ViewBag.OrderID = id;
			return View(orderDetails);
		}

		// ✅ 使用綠界官方測試環境 
		[HttpPost]
		public IActionResult GoToPayment(int orderId)
		{
			var order = _context.Orders.FirstOrDefault(o => o.OrderID == orderId);
			if (order == null) return NotFound();

			// 商店訂單編號 (確保唯一)
			string merchantTradeNo = $"B{DateTime.Now:yyMMddHHmm}{order.OrderID}";

			var ecpay = new ECPayRequest
			{
				MerchantID = "3002607",  // 測試商店代號
				MerchantTradeNo = merchantTradeNo,
				MerchantTradeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
				TotalAmount = Math.Max(1, (int)Math.Ceiling(order.TotalAmount)),
				TradeDesc = "BookLoop 書籍付款",
				ItemName = "書籍或租借",
				ReturnURL = "https://yourdomain/api/ecpay/notify",         // Server 通知 URL
				OrderResultURL = "https://yourdomain/Orders/PaymentResult", // 前端導回 URL
				ChoosePayment = "ALL",
				EncryptType = "1"
			};

			string postForm = ECPayHelper.GeneratePostForm(ecpay);

			Console.WriteLine($"[GoToPayment] called for orderId={orderId} at {DateTime.UtcNow}");
			Console.WriteLine($"MerchantID: {ecpay.MerchantID}");
			Console.WriteLine($"MerchantTradeNo: {ecpay.MerchantTradeNo}");
			Console.WriteLine($"MerchantTradeDate: {ecpay.MerchantTradeDate}");
			Console.WriteLine($"TotalAmount: {ecpay.TotalAmount}");
			Console.WriteLine($"ReturnURL: {ecpay.ReturnURL}");

			return Content(postForm, "text/html");
		}

		// ✅ 綠界付款完成通知
		[HttpPost]
		[Route("api/ecpay/notify")]
		public IActionResult ECPayNotify([FromForm] ECPayRequest data)
		{
			if (string.IsNullOrWhiteSpace(data?.CheckMacValue))
				return Content("0|NoCheckMacValue");

			if (!ECPayHelper.VerifyNotification(data))
				return Content("0|ErrorCheckMacValue");

			// 驗證成功 -> 更新訂單狀態
			int orderId = -1;
			var digits = new string(data.MerchantTradeNo.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
			if (int.TryParse(digits, out var parsedId))
				orderId = parsedId;

			if (orderId > 0)
			{
				var order = _context.Orders.FirstOrDefault(o => o.OrderID == orderId);
				if (order != null)
				{
					order.Status = 1; // 訂單付款完成狀態
					_context.SaveChanges();
				}
			}

			return Content("1|OK"); // 回覆綠界
		}

		// ✅ 付款完成前端導回頁面
		[HttpGet]
		public IActionResult PaymentResult()
		{
			ViewBag.Message = "付款完成！感謝您的訂購。";
			return View();
		}

	}
}
