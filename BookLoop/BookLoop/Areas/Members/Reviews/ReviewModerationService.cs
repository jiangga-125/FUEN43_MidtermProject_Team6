// Services/Reviews/ReviewModerationService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BookLoop.Areas.Reviews.Rules;
using BookLoop.Models;

namespace BookLoop.Areas.Reviews
{
	public interface IReviewModerationService
	{
		Task<(bool ok, string? message, AutoDecision decision)> AutoModerateAndPersistAsync(int reviewId);
		Task<(bool ok, string? message)> AdminApproveAsync(int reviewId, int adminId, string? reason = null);
		Task<(bool ok, string? message)> AdminRejectAsync(int reviewId, int adminId, string reason);
	}

	public class ReviewModerationService : IReviewModerationService
	{
		private readonly MemberContext _db;
		private readonly IReviewRulePipeline _pipeline;

		public ReviewModerationService(MemberContext db, IReviewRulePipeline pipeline)
		{
			_db = db;
			_pipeline = pipeline;
		}

		public async Task<(bool ok, string? message, AutoDecision decision)> AutoModerateAndPersistAsync(int reviewId)
		{
			var review = await _db.Reviews.FirstOrDefaultAsync(x => x.ReviewId == reviewId);
			if (review == null) return (false, "找不到評論", AutoDecision.Rejected);

			// 準備上下文（這裡可查會員信用分、24h 貼文數等）
			var ctx = new ReviewContext
			{
				MemberID = review.MemberId,
				TargetType = review.TargetType,
				TargetID = review.TargetId,
				Rating = review.Rating,
				Content = review.Content,
				ImageUrls = review.ImageUrls,
				MemberReputation = 0, // TODO: 查你的會員分數
				MemberReviewCountLast24h = await _db.Reviews
					.CountAsync(r => r.MemberId == review.MemberId &&
									 r.CreatedAt >= DateTime.UtcNow.AddHours(-24))
			};

			var pr = _pipeline.Run(ctx);

			// 寫入審核紀錄
			var reasons = string.Join(" | ", pr.Findings.Select(f => $"[{f.Severity}] {f.RuleName}: {f.Message}"));
			_db.ReviewModerations.Add(new ReviewModeration
			{
				ReviewId = review.ReviewId,
				Decision = (byte)pr.Decision,
				Reasons = reasons,
				ReviewedBy = null, // 自動審核
				RuleSnapshot = pr.RuleVersionSnapshot,
				ReviewedAt = DateTime.UtcNow
			});

			// 更新 Review 狀態
			switch (pr.Decision)
			{
				case AutoDecision.AutoPass:
					review.Status = 1; // Approved
					break;
				case AutoDecision.NeedsManual:
					review.Status = 0; // Pending
					break;
				case AutoDecision.Rejected:
					review.Status = 2; // Rejected
					break;
			}
			review.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();
			return (true, null, pr.Decision);
		}

		public async Task<(bool ok, string? message)> AdminApproveAsync(int reviewId, int adminId, string? reason = null)
		{
			var review = await _db.Reviews.FirstOrDefaultAsync(x => x.ReviewId == reviewId);
			if (review == null) return (false, "找不到評論");
			review.Status = 1; // Approved
			review.UpdatedAt = DateTime.UtcNow;

			_db.ReviewModerations.Add(new ReviewModeration
			{
				ReviewId = reviewId,
				Decision = 3, // ApprovedByAdmin
				Reasons = reason,
				ReviewedBy = adminId,
				ReviewedAt = DateTime.UtcNow,
				RuleSnapshot = "" // 可選
			});

			await _db.SaveChangesAsync();
			return (true, null);
		}

		public async Task<(bool ok, string? message)> AdminRejectAsync(int reviewId, int adminId, string reason)
		{
			var review = await _db.Reviews.FirstOrDefaultAsync(x => x.ReviewId == reviewId);
			if (review == null) return (false, "找不到評論");
			review.Status = 2; // Rejected
			review.UpdatedAt = DateTime.UtcNow;

			_db.ReviewModerations.Add(new ReviewModeration
			{
				ReviewId = reviewId,
				Decision = 4, // RejectedByAdmin
				Reasons = reason,
				ReviewedBy = adminId,
				ReviewedAt = DateTime.UtcNow,
				RuleSnapshot = ""
			});

			await _db.SaveChangesAsync();
			return (true, null);
		}
	}
}
