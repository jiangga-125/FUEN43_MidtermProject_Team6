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
			// 系統審核清單
			var autoList = await _db.ReviewModerations
	.Join(_db.Reviews,
		mod => mod.ReviewID,
		rev => rev.ReviewID,
		(mod, rev) => new { mod, rev })  // ✅ 建立匿名物件
	.Where(x => x.mod.Decision == 0)     // ✅ 用 x.mod
	.Select(x => new ReviewModerationUnifiedVM
	{
		ReviewID = x.rev.ReviewID,
		Content = x.rev.Content,
		Reason = x.mod.Reasons,
		Source = "系統審核",
		CreatedAt = x.rev.CreatedAt
	})
	.ToListAsync();


			// 會員檢舉清單
			var reportList = await _db.ReviewReports
				.Include(r => r.Review)
				.Include(r => r.Reporter)
				.Where(r => r.Status == 0)
				.Select(r => new ReviewModerationUnifiedVM
				{
					ReviewID = r.ReviewID,
					Content = r.Review.Content,
					Reason = r.Reason,
					ReporterName = r.Reporter.Username,
					Source = "會員檢舉",
					CreatedAt = r.CreatedAt
				}).ToListAsync();

			var all = autoList.Concat(reportList)
				.OrderByDescending(x => x.CreatedAt)
				.ToList();

			return View(all); // ✅ 傳給 View 的是 ReviewModerationUnifiedVM
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
				MemberID = vm.MemberID,
				TargetType = vm.TargetType,
				TargetID = 0,
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
			var (ok, message, decision) = await _mod.AutoModerateAndPersistAsync(review.ReviewID);

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

		[HttpGet]
		public IActionResult Report(int reviewId)
		{
			ViewBag.ReviewId = reviewId;
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Report(ReviewReport report)
		{
			if (!ModelState.IsValid)
				return View(report);

			// 模擬目前登入會員（正式版本請改成登入系統抓ID）
			int currentMemberId = 1;
			report.ReporterID = currentMemberId;

			// 檢查是否已經檢舉過這則評論
			bool exists = await _db.ReviewReports
				.AnyAsync(r => r.ReviewID == report.ReviewID && r.ReporterID == currentMemberId);

			if (exists)
			{
				TempData["Msg"] = "您已經檢舉過這則評論。";
				return RedirectToAction("PendingList");
			}

			report.CreatedAt = DateTime.Now;
			_db.ReviewReports.Add(report);
			await _db.SaveChangesAsync();

			TempData["Msg"] = "檢舉已送出，等待管理者審核。";
			return RedirectToAction("PendingList");
		}


	}
}
