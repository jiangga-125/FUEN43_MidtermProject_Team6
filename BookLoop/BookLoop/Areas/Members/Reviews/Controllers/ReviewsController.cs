// Controllers/ReviewsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Areas.Reviews;
using BookLoop.Models;
using BookLoop.Models.ViewModels;

namespace BookLoop.Areas.Reviews.Controllers
{
	[Route("[controller]/[action]")]
	public class ReviewsController : Controller
	{
		private readonly MemberContext _db;
		private readonly IReviewModerationService _mod;

		public ReviewsController(MemberContext db, IReviewModerationService mod)
		{
			_db = db;
			_mod = mod;
		}

		[HttpGet]
		public IActionResult Create()
		{
			return View(new CreateReviewVm()); // 送一個空VM到表單
		}

		// 會員送出評論（最小示例）
		[HttpPost]
		public async Task<IActionResult> Create(int memberId, byte targetType, int targetId, byte rating, string content)
		{
			var review = new Review
			{
				MemberId = memberId,
				TargetType = targetType,
				TargetId = targetId,
				Rating = rating,
				Content = content,
				Status = 0, // 先 Pending
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
			_db.Reviews.Add(review);
			await _db.SaveChangesAsync();

			// 走自動審核
			var (ok, msg, decision) = await _mod.AutoModerateAndPersistAsync(review.ReviewId);
			if (!ok) return BadRequest(msg);

			// 回應前端讓你決定 UI 行為
			return Ok(new { ReviewID = review.ReviewId, Decision = decision.ToString(), review.Status });
		}

		// 後台：待審清單
		[HttpGet]
		public async Task<IActionResult> PendingList()
		{
			var items = await _db.Reviews
				.Where(r => r.Status == 0)
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();
			return View(items);
		}

		// 後台：管理員通過
		[HttpPost]
		public async Task<IActionResult> Approve(int reviewId, int adminId, string? reason = null)
		{
			var (ok, msg) = await _mod.AdminApproveAsync(reviewId, adminId, reason);
			if (!ok) return BadRequest(msg);
			return Ok(new { ok = true });
		}

		// 後台：管理員拒絕
		[HttpPost]
		public async Task<IActionResult> Reject(int reviewId, int adminId, string reason)
		{
			var (ok, msg) = await _mod.AdminRejectAsync(reviewId, adminId, reason);
			if (!ok) return BadRequest(msg);
			return Ok(new { ok = true });
		}
	}
}
