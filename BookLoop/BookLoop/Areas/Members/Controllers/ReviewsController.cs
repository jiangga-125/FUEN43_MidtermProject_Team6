using BookLoop.Areas.Reviews;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Models.ViewModels;
using BookLoop.Services.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
				return View(vm);
			}

			// Step 1️⃣ 先建立 Review 並儲存（還沒審核）
			var review = new Review
			{
				MemberId = vm.MemberID,
				TargetType = vm.TargetType,
				TargetId = 0,
				Rating = vm.Rating,
				Content = vm.Content,
				Status = 0, // 0 = 待審
				ImageUrls = vm.TargetType == 1 ? vm.TargetBookName : vm.TargetMemberNickname,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			_db.Reviews.Add(review);
			await _db.SaveChangesAsync();

			// Step 2️⃣ 呼叫自動審核服務（這裡會跑 ForbiddenKeywordsRule 等所有規則）
			var (ok, message, decision) = await _mod.AutoModerateAndPersistAsync(review.ReviewId);

			// Step 3️⃣ 根據審核結果決定顯示內容
			if (!ok)
			{
				ModelState.AddModelError("", message ?? "審核失敗");
				return View(vm);
			}

			switch (decision)
			{
				case AutoDecision.Rejected:
					ModelState.AddModelError("", "評論未通過審核（含有不當內容）");
					return View(vm);

				case AutoDecision.NeedsManual:
					TempData["Msg"] = "評論送出成功，等待人工審核";
					break;

				case AutoDecision.AutoPass:
					TempData["Msg"] = "評論送出成功！已自動通過審核";
					break;
			}

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
