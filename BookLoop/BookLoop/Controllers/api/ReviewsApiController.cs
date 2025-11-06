using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Models.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookLoop.Controllers.api
{
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	[ApiController]
	[Route("api/[controller]/[action]")]
	public class ReviewsApiController : ControllerBase
	{
		private readonly OrdersysContext _ordersDb;
		private readonly MemberContext _memberDb;

		public ReviewsApiController(OrdersysContext ordersDb, MemberContext memberDb)
		{
			_ordersDb = ordersDb;
			_memberDb = memberDb;
		}

		// ✅ 查詢會員已完成訂單的書籍
		[HttpGet("{memberId}")]
		public async Task<IActionResult> GetPurchasedBooks(int memberId)
		{
			const int CompletedStatus = 1; // 1=已完成

			// 🔍 確認會員存在
			var exists = await _memberDb.Members.AnyAsync(m => m.MemberID == memberId);
			if (!exists)
				return NotFound(new { message = $"找不到會員 ID: {memberId}" });

			// ✅ 查詢該會員所有已完成訂單的書籍
			var books = await (
				from o in _ordersDb.Orders
				join od in _ordersDb.OrderDetails on o.OrderID equals od.OrderID
				join b in _ordersDb.Books on od.BookID equals b.BookID
				where o.MemberID == memberId && o.Status == CompletedStatus
				select new { b.BookID, b.Title }
			).Distinct().ToListAsync();

			if (!books.Any())
				return Ok(new { message = "尚未有可評論的書籍", data = new List<object>() });

			return Ok(books.Select(b => new
			{
				bookId = b.BookID,
				title = b.Title
			}));


		}

		// ✅ 建立評論
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateReviewVm vm)
		{
			try
			{
				Console.WriteLine("📥 收到建立評論請求");

				if (!ModelState.IsValid)
				{
					Console.WriteLine("❌ ModelState 驗證未通過");
					return BadRequest(new { message = "欄位驗證未通過", errors = ModelState });
				}

				Console.WriteLine($"📦 前端傳入資料：MemberID={vm.MemberID}, TargetBookID={vm.TargetBookID}, Rating={vm.Rating}, Content={vm.Content}");

				// 🔐 先試著從 JWT 拿會員 ID
				// 嘗試從 JWT 取 MemberId Claim
				var memberIdClaim = User.FindFirstValue("mid"); // ← 注意這裡是 "MemberId"
				int memberId;

				if (string.IsNullOrEmpty(memberIdClaim))
				{
					Console.WriteLine("⚠️ JWT 沒有 MemberId，暫時使用前端傳來的 MemberID");
					memberId = vm.MemberID; // 🔹 測試階段用前端傳的
				}
				else
				{
					memberId = int.Parse(memberIdClaim);
				}

				Console.WriteLine($"✅ 使用的會員 ID：{memberId}");


				// 檢查會員存在
				bool memberExists = await _memberDb.Members.AnyAsync(m => m.MemberID == memberId);
				if (!memberExists)
				{
					Console.WriteLine($"❌ 找不到會員 ID: {memberId}");
					return NotFound(new { message = $"找不到會員 ID: {memberId}" });
				}

				// 🔎 檢查是否真的買過該書
				const int CompletedStatus = 1;
				bool purchased = await (
					from o in _ordersDb.Orders
					join od in _ordersDb.OrderDetails on o.OrderID equals od.OrderID
					where o.MemberID == memberId && o.Status == CompletedStatus && od.BookID == vm.TargetBookID
					select od
				).AnyAsync();

				Console.WriteLine($"🧾 書籍購買狀態檢查：{purchased}");

				if (!purchased)
				{
					Console.WriteLine("🚫 此書籍不在會員的已完成訂單中");
					return BadRequest(new { message = "此書籍不在您的已完成訂單中，無法評論。" });
				}

				// ✅ 新增評論
				var review = new Review
				{
					MemberID = memberId,
					TargetType = 1,
					TargetID = vm.TargetBookID,
					Rating = vm.Rating,
					Content = vm.Content,
					Status = 0,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};

				_memberDb.Reviews.Add(review);
				await _memberDb.SaveChangesAsync();

				Console.WriteLine($"✅ 成功建立評論：ReviewID={review.ReviewID}");

				return Ok(new { message = "✅ 評論已送出，等待管理員審核。" });
			}
			catch (Exception ex)
			{
				Console.WriteLine("❌ 伺服器發生例外：" + ex.Message);
				Console.WriteLine(ex.StackTrace);
				return StatusCode(500, new { message = "伺服器發生錯誤", detail = ex.Message });
			}
		}


	}
}
