using BookLoop.Areas.Reviews;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models.ViewModels;
using BookLoop.Data;

namespace BookLoop.Controllers
{

	[Area("Members")]
	public class ReviewsController : Controller
	{
		private readonly MemberContext _db;
		private readonly IReviewModerationService _mod;

		public ReviewsController(MemberContext db, IReviewModerationService mod)
		{
			_db = db; _mod = mod;
		}

		[HttpGet]
		public async Task<IActionResult> PendingList()
		{
			var items = await _db.Reviews
				.Where(r => r.Status == 0)
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();
			return View(items);
		}

		[HttpGet]
		public IActionResult Create() => View(); // 對應 Create.cshtml

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreateReviewVm vm)
		{
			if (!ModelState.IsValid)
			{
				return View(vm); // 驗證失敗 → 回到表單顯示錯誤訊息
			}

			var review = new Review
			{
				MemberId = vm.MemberID,
				TargetType = vm.TargetType,
				TargetId = 0, // 如果不想用數字 ID，可以固定給 0
				Rating = vm.Rating,
				Content = vm.Content,
				Status = 0,
				ImageUrls = vm.TargetType == 1 ? vm.TargetBookName : vm.TargetMemberNickname, // 改用途存名稱
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};


			_db.Reviews.Add(review);
			await _db.SaveChangesAsync();

			TempData["Msg"] = "評論送出成功，等待審核";
			return RedirectToAction(nameof(Create));
		}


		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Approve(int reviewId, int adminId, string? reason = null)
		{
			// 1. 找到評論
			var review = await _db.Reviews.FindAsync(reviewId);
			if (review == null)
			{
				TempData["Error"] = "找不到評論";
				return RedirectToAction("PendingList");
			}

			// 2. 更新狀態
			review.Status = 1; // 假設 1 = 已通過 (你可以用 Enum 定義會更清楚)
			review.UpdatedAt = DateTime.Now;

			// 3. 儲存進資料庫
			await _db.SaveChangesAsync();

			// 4. 顯示提示訊息
			TempData["Msg"] = $"評論 {reviewId} 已通過！";
			return RedirectToAction("PendingList");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reject(int reviewId, int adminId, string reason)
		{
			var review = await _db.Reviews.FindAsync(reviewId);
			if (review == null)
			{
				TempData["Msg"] = "找不到評論";
				return RedirectToAction("PendingList");
			}

			_db.Reviews.Remove(review);
			await _db.SaveChangesAsync();

			TempData["Msg"] = $"評論 {reviewId} 已被刪除！理由：{reason}";
			return RedirectToAction("PendingList");
		}
	}
}
