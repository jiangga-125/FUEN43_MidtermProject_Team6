using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace BookLoop.Controllers.Api
{
	[Route("api/[controller]")]
	[ApiController]
	public class ShipmentController : ControllerBase
	{
		private readonly OrdersysContext _db;

		public ShipmentController(OrdersysContext db)
		{
			_db = db;
		}

		// ✅ 查詢單筆訂單的物流資訊
		[HttpGet("{orderId}")]
		public async Task<IActionResult> GetShipment(int orderId)
		{
			var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.OrderID == orderId);
			if (shipment == null)
				return NotFound(new { success = false, message = "找不到物流資訊" });

			return Ok(new { success = true, shipment });
		}

		// ✅ 建立或更新物流資訊（確保只有一筆，狀態永遠是待出貨）
		[HttpPost("create")]
		public async Task<IActionResult> CreateShipment([FromBody] ShipmentCreateRequest req)
		{
			Console.WriteLine("✅ 收到建立物流請求：" + System.Text.Json.JsonSerializer.Serialize(req));

			if (req == null)
				return BadRequest(new { success = false, message = "請提供建立物流的資料" });

			if (req.OrderID == 0 || string.IsNullOrEmpty(req.Provider))
				return BadRequest(new { success = false, message = "OrderID 與 Provider 為必填欄位" });

			// 🔍 檢查是否已有物流紀錄
			var existingShipment = await _db.Shipments.FirstOrDefaultAsync(s => s.OrderID == req.OrderID);

			if (existingShipment != null)
			{
				// 🛠 若已存在 → 更新 Provider、狀態重設為待出貨
				existingShipment.Provider = req.Provider;
				existingShipment.Status = 0; // 強制重設為待出貨
				existingShipment.UpdatedAt = DateTime.UtcNow;

				await _db.SaveChangesAsync();
				Console.WriteLine($"🚚 已更新物流：OrderID={req.OrderID}, Provider={req.Provider}");
				return Ok(new { success = true, message = "物流更新成功（原物流已覆蓋）", shipment = existingShipment });
			}

			// 🔹 第一次建立：生成追蹤號碼
			var trackingNumber = $"TRK{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

			var shipment = new Shipment
			{
				OrderID = req.OrderID,
				Provider = req.Provider,
				TrackingNumber = trackingNumber,
				Status = 0, // 待出貨
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			_db.Shipments.Add(shipment);
			await _db.SaveChangesAsync();

			Console.WriteLine($"📦 已建立物流：OrderID={req.OrderID}, Provider={req.Provider}, Tracking={trackingNumber}");
			return Ok(new { success = true, message = "物流建立成功", shipment });
		}

		// ✅ 更新物流（明確更新接口）
		[HttpPost("update/{shipmentId}")]
		public async Task<IActionResult> UpdateShipment(int shipmentId, [FromBody] Shipment update)
		{
			Console.WriteLine("🛠 更新物流：" + shipmentId);
			Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(update));

			var shipment = await _db.Shipments.FindAsync(shipmentId);
			if (shipment == null)
				return NotFound(new { success = false, message = "找不到物流資料" });

			// 🔹 只更新非空欄位，狀態固定為待出貨
			shipment.Provider = update.Provider ?? shipment.Provider;
			shipment.TrackingNumber = shipment.TrackingNumber; // 保持不變
			shipment.Status = 0; // 強制維持「待出貨」
			shipment.ShippedDate = shipment.ShippedDate;
			shipment.DeliveredDate = shipment.DeliveredDate;
			shipment.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();

			Console.WriteLine($"🔁 已修改物流：ShipmentID={shipmentId}, Provider={shipment.Provider}");
			return Ok(new { success = true, message = "物流更新成功", shipment });
		}
	}
}
