using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.ReviewEnums;

namespace BookLoop.Services.Reviews
{
	/// 輕量規則引擎：讀取最新一筆 ReviewRuleSettings，對 Review 做檢查，回 Findings 與快照 JSON
	public class ReviewRuleEngine
	{
		private readonly MemberContext _db;
		public ReviewRuleEngine(MemberContext db) => _db = db;

		public sealed class Finding
		{
			public string Code { get; set; } = default!;
			public string Message { get; set; } = default!;
			public bool Block { get; set; }
		}

		public async Task<(IList<Finding> Findings, string SnapshotJson)> EvaluateAsync(Review review)
		{
			var set = await _db.ReviewRuleSettings.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync();
			var findings = new List<Finding>();

			if (set == null)
			{
				return (new List<Finding> {
					new Finding { Code="NO_SETTINGS", Message="未設定審核規則。", Block=false }
				}, "{}");
			}

			// 1) 內容長度
			if (!string.IsNullOrWhiteSpace(review.Content) && review.Content.Length < set.MinContentLength)
				findings.Add(new Finding { Code = "MIN_LENGTH", Message = $"內容至少 {set.MinContentLength} 字。", Block = true });

			// 2) 評分範圍
			if (review.Rating < set.RatingMin || review.Rating > set.RatingMax)
				findings.Add(new Finding { Code = "RATING_RANGE", Message = $"評分需介於 {set.RatingMin}~{set.RatingMax}。", Block = true });

			// 3) 禁止自評（當 TargetTypeForMember 命中時）
			if (set.BlockSelfReview && review.TargetType == set.TargetTypeForMember && review.MemberId == review.TargetId)
				findings.Add(new Finding { Code = "SELF_REVIEW", Message = "禁止對自己評論。", Block = true });

			// 4) 禁止 URL
			if (set.ForbidUrls && Regex.IsMatch(review.Content ?? "", @"https?://", RegexOptions.IgnoreCase))
				findings.Add(new Finding { Code = "URL_FORBIDDEN", Message = "內容不可包含網址。", Block = true });

			// 5) 禁字
			if (!string.IsNullOrWhiteSpace(set.ForbiddenKeywords))
			{
				var kws = set.ForbiddenKeywords
					.Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim()).Where(s => s.Length > 0);
				foreach (var kw in kws)
				{
					if ((review.Content ?? "").IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
						findings.Add(new Finding { Code = "FORBIDDEN_KEYWORD", Message = $"含禁字：{kw}", Block = true });
				}
			}

			// 6) 重複評論（時窗內相同內容）
			if (set.DuplicateWindowHours > 0)
			{
				var from = DateTime.UtcNow.AddHours(-set.DuplicateWindowHours);
				var dup = await _db.Reviews.AnyAsync(r =>
					r.MemberId == review.MemberId &&
					r.TargetType == review.TargetType &&
					r.TargetId == review.TargetId &&
					r.CreatedAt >= from &&
					r.Content == review.Content
				);
				if (dup)
				{
					var block = set.DuplicatePolicy == DuplicatePolicyCodes.ForbidWithinWindow;
					findings.Add(new Finding { Code = "DUP_CONTENT", Message = $"{set.DuplicateWindowHours} 小時內出現相同內容。", Block = block });
				}
			}

			var snap = $"{{\"UpdatedAt\":\"{set.UpdatedAt:o}\",\"MinContentLength\":{set.MinContentLength},\"RatingMin\":{set.RatingMin},\"RatingMax\":{set.RatingMax}}}";
			return (findings, snap);
		}
	}
}
