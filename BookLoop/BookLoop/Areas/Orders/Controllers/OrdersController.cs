using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.EntityFrameworkCore;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

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
				TotalAmount = 0 ,// 預設總金額
				 OrderDate = DateTime.Today
			};
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("OrderID,MemberID,OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount")] Order order,
string Provider)
		{
			if (ModelState.IsValid)
			{
				order.TotalAmount = order.OrderDetails?.Sum(od => od.UnitPrice * od.Quantity) ?? 0;

				order.CreatedAt = DateTime.UtcNow;
				_context.Add(order);
				await _context.SaveChangesAsync();

				// 新增物流
				if (!string.IsNullOrEmpty(Provider))
				{
					var shipment = new Shipment
					{
						OrderID = order.OrderID,
						Provider = Provider,
						TrackingNumber = $"TN{DateTime.Now:yyyyMMddHHmmss}{order.OrderID}", // 自動生成運單號
						Status = 0, // 預設未出貨
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow
					};
					_context.Shipments.Add(shipment);
					await _context.SaveChangesAsync();
				}


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
		public async Task<IActionResult> Edit(int id, [Bind("OrderDate,TotalAmount,Status,DiscountAmount,DiscountCode,MemberCouponID,CouponTypeSnap,CouponValueSnap,CouponNameSnap,CouponDiscountAmount,MemberID")] Order orderInput,
	string Provider, byte? ShipmentStatus)
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

			// 新增物流
			var shipment = order.Shipments.FirstOrDefault();
			if (!string.IsNullOrEmpty(Provider))
			{
				if (shipment == null)
				{
					shipment = new Shipment
					{
						OrderID = order.OrderID,
						TrackingNumber = $"TN{DateTime.Now:yyyyMMddHHmmss}{order.OrderID}",
						CreatedAt = DateTime.UtcNow
					};
					_context.Shipments.Add(shipment);
				}

				shipment.Provider = Provider;
				shipment.Status = ShipmentStatus ?? 0;
				shipment.UpdatedAt = DateTime.UtcNow;
			}
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
			// 先抓訂單並 Include OrderDetails
			var order = _context.Orders
				.Include(o => o.OrderDetails)
				.FirstOrDefault(o => o.OrderID == orderId);

			if (order == null)
				return NotFound();



			// 重新計算總金額
			decimal totalAmount = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);
			int amountToPay = (int)Math.Round(totalAmount, MidpointRounding.AwayFromZero);
			if (amountToPay <= 0) amountToPay = 1;

			string merchantTradeNo = $"B{DateTime.Now:yyMMddHHmmssfff}{order.OrderID}";
			string website = "https://localhost:7176";
			// ⚠️ 改成你的實際網域（或 localhost 測試）

			var ecpayRequest = new ECPayRequest
			{
				MerchantID = "3002607",
				MerchantTradeNo = merchantTradeNo,
				MerchantTradeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
				PaymentType = "aio",
				TotalAmount = amountToPay,
				TradeDesc = "BookLoop 書籍付款",
				ItemName = string.Join("#", order.OrderDetails.Select(od => od.ProductName)), // 可顯示所有商品
				ReturnURL = $"{website}/api/ecpay/notify",
				OrderResultURL = $"{website}/Orders/Orders/PaymentResult?orderId={order.OrderID}",
				ChoosePayment = "ALL",
				EncryptType = "1"
			};

			// 生成 CheckMacValue
			ecpayRequest.CheckMacValue = ECPayHelper.GenerateCheckMacValue(ecpayRequest);

			// 將資料轉成 Dictionary 給 View 自動送出表單
			var orderDict = new Dictionary<string, string>
	{
		{ "MerchantID", ecpayRequest.MerchantID },
		{ "MerchantTradeNo", ecpayRequest.MerchantTradeNo },
		{ "MerchantTradeDate", ecpayRequest.MerchantTradeDate },
		{ "PaymentType", ecpayRequest.PaymentType },
		{ "TotalAmount", ecpayRequest.TotalAmount.ToString() },
		{ "TradeDesc", ecpayRequest.TradeDesc },
		{ "ItemName", ecpayRequest.ItemName },
		{ "ReturnURL", ecpayRequest.ReturnURL },
		{ "OrderResultURL", ecpayRequest.OrderResultURL },
		{ "ChoosePayment", ecpayRequest.ChoosePayment },
		{ "EncryptType", ecpayRequest.EncryptType },
		{ "CheckMacValue", ecpayRequest.CheckMacValue }
	};

			return View("GoToPayment", orderDict);
		}
		//
		// ✅ 綠界付款完成通知 (Server -> Server)
		//
		[HttpPost]
		[Route("api/ecpay/notify")]
		public IActionResult ECPayNotify([FromForm] ECPayRequest data)
		{
			if (string.IsNullOrWhiteSpace(data?.CheckMacValue))
				return Content("0|NoCheckMacValue");

			if (!ECPayHelper.VerifyNotification(data))
				return Content("0|ErrorCheckMacValue");

			// === step 4 : 更新訂單狀態 ===
			int orderId = -1;
			var digits = new string(data.MerchantTradeNo.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
			if (int.TryParse(digits, out var parsedId))
				orderId = parsedId;

			if (orderId > 0)
			{
				var order = _context.Orders.FirstOrDefault(o => o.OrderID == orderId);
				if (order != null)
				{
					order.Status = 1; // ✅ 訂單付款完成狀態
					_context.SaveChanges();
				}
			}

			return Content("1|OK"); // 回覆綠界「成功」
		}

		//
		// ✅ 付款完成導回頁面
		//
		[HttpGet, HttpPost]
		public IActionResult PaymentResult(int orderId)
		{
			var order = _context.Orders.FirstOrDefault(o => o.OrderID == orderId);
			if (order != null && order.Status != 1)
			{
				order.Status = 1; // 強制設為已付款
				_context.SaveChanges();
			}

			ViewBag.Message = "付款完成！感謝您的訂購。";
			return View();
		}




	}
}