using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookLoop.Controllers.Api
{
	[Route("api/[controller]")]
	[ApiController]
	public class OrderController : ControllerBase
	{
		private readonly ShopDbContext _db;
		private readonly MemberContext _memberCtx;
		public OrderController(ShopDbContext db, MemberContext memberCtx)
		{
			_db = db;
			_memberCtx = memberCtx;
		}

		public class CreateOrderRequest
		{
			public Order Order { get; set; } = null!;
			public string? CouponCode { get; set; }
			public int? MemberCouponId { get; set; }
		}

		public class PaymentRequest
		{
			public int OrderID { get; set; }
		}

		// ✅ 建立訂單
		[HttpPost("create")]
		public async Task<IActionResult> CreateOrder([FromBody] JsonElement body)
		{
			// parse body: 支援兩種格式
			CreateOrderRequest? req = null;
			Order? order = null;
			string? couponCode = null;
			int? memberCouponId = null;

			try
			{
				// 若外層有 "Order" 屬性 => 新版 wrapper（Order + CouponCode / MemberCouponId）
				if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("Order", out var _))
				{
					req = JsonSerializer.Deserialize<CreateOrderRequest>(body.GetRawText(), new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					order = req?.Order;
					couponCode = req?.CouponCode;
					memberCouponId = req?.MemberCouponId;
				}
				else
				{
					// MODIFIED: 即便是當作 "純 Order" 解析，我們也嘗試從外層抓 CouponCode 與 MemberCouponId
					if (body.ValueKind == JsonValueKind.Object)
					{
						// ADDED: 若有 CouponCode 屬性（頂層），先讀出來
						if (body.TryGetProperty("CouponCode", out var cp) && cp.ValueKind == JsonValueKind.String)
						{
							couponCode = cp.GetString();
						}
						// ADDED: 若有 MemberCouponId 屬性（頂層），先讀出來
						if (body.TryGetProperty("MemberCouponId", out var mc) && (mc.ValueKind == JsonValueKind.Number || mc.ValueKind == JsonValueKind.String))
						{
							// Try parse int
							if (mc.ValueKind == JsonValueKind.Number && mc.TryGetInt32(out var mid))
								memberCouponId = mid;
							else if (mc.ValueKind == JsonValueKind.String && int.TryParse(mc.GetString(), out var mid2))
								memberCouponId = mid2;
						}
					}

					// 然後再把整個 body 當 Order 反序列化（兼容舊版）
					order = JsonSerializer.Deserialize<Order>(body.GetRawText(), new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
				}
			}
			catch (Exception ex)
			{
				return BadRequest(new { success = false, message = "Request body 解析失敗", detail = ex.Message });
			}

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

			// subtotal 與 coupon 計算
			decimal subtotal = order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity);
			decimal discount = 0m;
			Coupon? couponToUse = null;
			MemberCoupon? memberCouponRow = null;

			// ADDED: 若有 memberCouponId，優先使用 member coupon（並載入對應 Coupon）
			if (memberCouponId.HasValue)
			{
				memberCouponRow = await _memberCtx.MemberCoupons
					.Include(mc => mc.Coupon)
					.FirstOrDefaultAsync(mc => mc.MemberCouponId == memberCouponId.Value && mc.MemberId == order.MemberID);

				if (memberCouponRow == null)
					return BadRequest(new { success = false, message = "會員優惠券不存在或不屬於此會員" });

				couponToUse = memberCouponRow.Coupon;
			}
			// ADDED: 若有 couponCode，查 Coupons 表
			else if (!string.IsNullOrWhiteSpace(couponCode))
			{
				var code = couponCode.Trim();
				couponToUse = await _memberCtx.Coupons.FirstOrDefaultAsync(c => c.Code == code && c.IsActive == true);
				if (couponToUse == null)
					return BadRequest(new { success = false, message = "優惠碼不存在或已停用" });
			}

			// coupon 驗證與計算折抵（你之前邏輯）
			if (couponToUse != null)
			{
				var now = DateTime.UtcNow;
				if (couponToUse.StartAt.HasValue && now < couponToUse.StartAt.Value)
					return BadRequest(new { success = false, message = "優惠券尚未生效" });
				if (couponToUse.EndAt.HasValue && now > couponToUse.EndAt.Value)
					return BadRequest(new { success = false, message = "優惠券已過期" });

				if (couponToUse.MinOrderAmount.HasValue && subtotal < couponToUse.MinOrderAmount.Value)
					return BadRequest(new { success = false, message = $"未達優惠門檻 {couponToUse.MinOrderAmount.Value} 元" });

				// 計算折扣（0 = 固定金額，其他視為百分比）
				if (couponToUse.DiscountType == 0)
					discount = couponToUse.DiscountValue;
				else
					discount = Math.Round(subtotal * (couponToUse.DiscountValue / 100m), 2);

				if (couponToUse.MaxDiscountAmount.HasValue)
					discount = Math.Min(discount, couponToUse.MaxDiscountAmount.Value);

				discount = Math.Min(discount, subtotal);
			}

			// MODIFIED: 把折抵資訊寫進 Order 的對應欄位（你 model 有這些欄位）
			order.CouponDiscountAmount = discount;      // 存 coupon 專屬折抵
			order.DiscountAmount = discount;            // 總折抵（保留原欄位）
			if (couponToUse != null)
			{
				order.DiscountCode = couponToUse.Code;           // 你 model 使用 DiscountCode
				order.CouponNameSnap = couponToUse.Name;         // snapshot 名稱
				order.CouponValueSnap = couponToUse.DiscountValue;
				order.CouponTypeSnap = (byte?)couponToUse.DiscountType;
			}
			if (memberCouponRow != null)
			{
				// MemberCouponId 在 model 是 long?，保險轉型
				order.MemberCouponID = Convert.ToInt64(memberCouponRow.MemberCouponId);
			}

			order.TotalAmount = Math.Max(0m, subtotal - discount);

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


		// ✅ 刪除訂單
		[HttpPost("delete/{orderId}")]
		public async Task<IActionResult> DeleteOrder(int orderId)
		{
			var order = await _db.Orders
				.Include(o => o.OrderDetails) // ✅ 同時載入明細，否則會有 FK 錯誤
				.FirstOrDefaultAsync(o => o.OrderID == orderId);

			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			// ✅ 先刪除子項（OrderDetails）
			_db.OrderDetails.RemoveRange(order.OrderDetails);

			// ✅ 再刪除主項（Order）
			_db.Orders.Remove(order);

			await _db.SaveChangesAsync();

			return Ok(new { success = true, message = "訂單已永久刪除" });
		}


		// ✅ 付款流程（綠界）
		[HttpPost("GoToPayment")]
		//[AllowAnonymous]
		public IActionResult GoToPayment([FromBody] PaymentRequest req)
		{
			int orderId = req.OrderID; // 從物件取得 OrderID
			var order = _db.Orders
				.Include(o => o.OrderDetails)
				.FirstOrDefault(o => o.OrderID == orderId);

			if (order == null)
				return NotFound(new { success = false, message = "找不到訂單" });

			decimal totalAmount = order.TotalAmount;
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


		// ✅ EcpayNotify（綠界）
		[HttpPost("/api/ecpay/notify")]
		[AllowAnonymous]
		public IActionResult EcpayNotify([FromForm] ECPayRequest data)
		{
			if (data == null || string.IsNullOrWhiteSpace(data.CheckMacValue))
				return Content("0|NoCheckMacValue");

			if (!ECPayHelper.VerifyNotification(data))
				return Content("0|ErrorCheckMacValue");

			// 從 MerchantTradeNo 解析 orderId（尾數取數字）
			int orderId = -1;
			var digits = new string(data.MerchantTradeNo.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
			if (!int.TryParse(digits, out orderId))
				return Content("0|ParseOrderIdFail");

			var order = _db.Orders.Include(o => o.OrderDetails).FirstOrDefault(o => o.OrderID == orderId);
			if (order == null) return Content("0|OrderNotFound");

			var expectedAmt = (int)Math.Round(order.OrderDetails.Sum(od => od.UnitPrice * od.Quantity), MidpointRounding.AwayFromZero);
			if (expectedAmt != data.TotalAmount) return Content("0|AmountMismatch");

			if (order.Status == 1) return Content("1|OK"); // idempotent

			// 更新訂單狀態（若有 TradeNo / PaidAt 欄位，可寫入）
			order.Status = 1;
			_db.SaveChanges();

			// ADDED: 若使用了 MemberCoupon，標記該 MemberCoupon 為已使用（保守處理 RedeemedAt 欄位）
			try
			{
				if (order.MemberCouponID.HasValue)
				{
					var mc = _memberCtx.MemberCoupons.FirstOrDefault(m => m.MemberCouponId == order.MemberCouponID.Value);
					if (mc != null)
					{
						mc.Status = 1; // 假設 1 = 已使用

						// SAFELY set RedeemedAt if property exists; otherwise try UpdatedAt
						var pi = mc.GetType().GetProperty("RedeemedAt");
						if (pi != null && pi.CanWrite)
						{
							pi.SetValue(mc, DateTime.UtcNow);
						}
						else
						{
							var pi2 = mc.GetType().GetProperty("UpdatedAt");
							if (pi2 != null && pi2.CanWrite) pi2.SetValue(mc, DateTime.UtcNow);
						}

						_memberCtx.MemberCoupons.Update(mc);
						_memberCtx.SaveChanges();
					}
				}
			}
			catch (Exception ex)
			{
				// LOG but don't block ecpay reply
				Console.WriteLine("Warning: 無法更新 MemberCoupon 為已使用 -> " + ex.Message);
			}

			return Content("1|OK");
		}

		[HttpGet("status/{orderId}")]
		public IActionResult GetStatus(int orderId)
		{
			var order = _db.Orders.Find(orderId);
			if (order == null) return NotFound(new { success = false, message = "找不到訂單" });
			return Ok(new { success = true, orderId = order.OrderID, status = order.Status, totalAmount = order.TotalAmount });
		}
	}
}
