using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers.Api
{
	[Route("api/[controller]")]
	[ApiController]
	public class OrderController : ControllerBase
	{
		private readonly ShopDbContext _db;

		public OrderController(ShopDbContext db)
		{
			_db = db;
		}

		// ✅ 建立訂單
		[HttpPost("create")]
		public async Task<IActionResult> CreateOrder([FromBody] Order order)
		{
			if (order?.OrderDetails == null || !order.OrderDetails.Any())
				return BadRequest(new { success = false, message = "訂單資料不完整" });

			order.CreatedAt = DateTime.UtcNow;

			var bookIds = order.OrderDetails.Select(od => od.BookID).Distinct().ToList();
			var books = await _db.Books
				.Where(b => bookIds.Contains(b.BookID))
				.ToDictionaryAsync(b => b.BookID);

			foreach (var od in order.OrderDetails)
			{
				od.CreatedAt = DateTime.UtcNow;
				od.Quantity = od.Quantity <= 0 ? 1 : od.Quantity;

				if (books.TryGetValue(od.BookID, out var book))
				{
					od.UnitPrice = od.UnitPrice <= 0 ? (book.SalePrice ?? 1m) : od.UnitPrice;
					od.ProductName = string.IsNullOrEmpty(od.ProductName) ? book.Title : od.ProductName;
				}
				else
				{
					return BadRequest(new { success = false, message = $"BookID {od.BookID} 不存在" });
				}
			}

			order.TotalAmount = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);

			_db.Orders.Add(order);
			await _db.SaveChangesAsync();

			return Ok(new { success = true, orderId = order.OrderID, totalAmount = order.TotalAmount });
		}

		// ✅ 取得會員所有訂單（排除已刪除）【匿名投影，避免循環引用】
		[HttpGet("member/{memberId}")]
		public async Task<IActionResult> GetOrdersByMember(int memberId)
		{
			try
			{
				var orders = await _db.Orders
					.Where(o => o.MemberID == memberId && o.Status != 9)
					.Include(o => o.OrderDetails)
						.ThenInclude(od => od.Book)
					.OrderByDescending(o => o.CreatedAt)
					.Select(o => new
					{
						o.OrderID,
						o.MemberID,
						o.TotalAmount,
						o.Status,
						o.CreatedAt,
						OrderDetails = o.OrderDetails.Select(od => new
						{
							od.BookID,
							od.Quantity,
							od.UnitPrice,
							BookTitle = od.Book != null ? od.Book.Title : "(已下架)"
						}).ToList()
					})
					.ToListAsync();

				return Ok(orders);
			}
			catch (Exception ex)
			{
				Console.WriteLine("GetOrdersByMember Error:", ex);
				return StatusCode(500, new { success = false, message = "伺服器錯誤" });
			}
		}

		// ✅ 取得單筆訂單明細（匿名投影）
		[HttpGet("{orderId}")]
		public async Task<IActionResult> GetOrderDetail(int orderId)
		{
			var order = await _db.Orders
				.Include(o => o.OrderDetails)
					.ThenInclude(od => od.Book)
				.Where(o => o.OrderID == orderId)
				.Select(o => new
				{
					o.OrderID,
					o.MemberID,
					o.TotalAmount,
					o.Status,
					o.CreatedAt,
					OrderDetails = o.OrderDetails.Select(od => new
					{
						od.BookID,
						od.Quantity,
						od.UnitPrice,
						BookTitle = od.Book != null ? od.Book.Title : "(已下架)"
					}).ToList()
				})
				.FirstOrDefaultAsync();

			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			return Ok(order);
		}

		// ✅ 取消訂單
		[HttpPost("cancel/{orderId}")]
		public async Task<IActionResult> CancelOrder(int orderId)
		{
			var order = await _db.Orders.FindAsync(orderId);
			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			order.Status = 0; // 取消
			await _db.SaveChangesAsync();

			return Ok(new { success = true, message = "訂單已取消" });
		}

		// ✅ 軟刪除訂單
		[HttpPost("delete/{orderId}")]
		public async Task<IActionResult> DeleteOrder(int orderId)
		{
			var order = await _db.Orders.FindAsync(orderId);
			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			order.Status = 9; // 軟刪除
			await _db.SaveChangesAsync();

			return Ok(new { success = true, message = "訂單已軟刪除" });
		}



		public class PaymentRequest
		{
			public int OrderID { get; set; }
		}
		// ✅ 付款流程（綠界）
		[HttpPost("GoToPayment")]
		public IActionResult GoToPayment([FromBody] PaymentRequest req)
		{
			int orderId = req.OrderID; // 從物件取得 OrderID
			var order = _db.Orders
				.Include(o => o.OrderDetails)
				.FirstOrDefault(o => o.OrderID == orderId);

			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			decimal totalAmount = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);
			int amountToPay = (int)Math.Round(totalAmount, MidpointRounding.AwayFromZero);
			if (amountToPay <= 0) amountToPay = 1;

			string merchantTradeNo = $"B{DateTime.Now:yyMMddHHmmssfff}{order.OrderID}";
			string website = "https://localhost:7176"; // 測試環境網址

			var ecpayRequest = new ECPayRequest
			{
				MerchantID = "3002607",
				MerchantTradeNo = merchantTradeNo,
				MerchantTradeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
				PaymentType = "aio",
				TotalAmount = amountToPay,
				TradeDesc = "BookLoop 書籍付款",
				ItemName = string.Join("#", order.OrderDetails.Select(od => od.ProductName)),
				ReturnURL = $"{website}/api/ecpay/notify",
				OrderResultURL = $"{website}/Orders/Orders/PaymentResult?orderId={order.OrderID}",
				ChoosePayment = "ALL",
				EncryptType = "1"
			};

			ecpayRequest.CheckMacValue = ECPayHelper.GenerateCheckMacValue(ecpayRequest);

			return Ok(ecpayRequest);
		}







	}
}
