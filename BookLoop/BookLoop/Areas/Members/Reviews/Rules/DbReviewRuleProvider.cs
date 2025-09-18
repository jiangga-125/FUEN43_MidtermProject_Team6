// Services/Reviews/Rules/DbReviewRuleProvider.cs
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;
using System.Collections.Generic;
using BookLoop.Areas.Reviews.Rules;

public class DbReviewRuleProvider : IReviewRuleProvider
{
	private readonly MemberContext _db;

	public DbReviewRuleProvider(MemberContext db) => _db = db;

	public IEnumerable<IReviewRule> GetRules()
	{
		// 讀單一設定（Id=1）
		var s = _db.ReviewRuleSettings.AsNoTracking().First(x => x.Id == 1);

		// 1) 字數
		yield return new MinLengthRule(s.MinContentLength);

		// 2) 評分範圍（用資料庫 CHECK 已擋，但也可在規則層加保險）
		yield return new RatingRangeRule(s.RatingMin, s.RatingMax);

		// 3) 禁網址/聯絡方式
		if (s.ForbidUrls) yield return new NoUrlOrContactRule();

		// 4) 禁自評（只在評會員時檢查）
		if (s.BlockSelfReview) yield return new NoSelfReviewRule(s.TargetTypeForMember);

		// 5) 敏感詞
		var list = (s.ForbiddenKeywords ?? "")
				   .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
				   .Select(x => x.Trim())
				   .Where(x => x.Length > 0)
				   .ToArray();

		if (!string.IsNullOrWhiteSpace(s.ForbiddenKeywords))
			yield return new ForbiddenKeywordsRule(s.ForbiddenKeywords);
		else
			yield return new ForbiddenKeywordsRule(); // 沒填時用預設


		// 6) 重複偵測（Warn 或 Block 由設定決定）
		if (s.DuplicatePolicy != 0)
		{
			var severity = s.DuplicatePolicy == 2 ? RuleSeverity.Block : RuleSeverity.Warn;
			yield return new RepeatedContentRule(
	check: (authorId, content) =>
		_db.Reviews.Any(r =>
			r.MemberId == authorId &&
			r.Content == content.Trim() &&
			r.CreatedAt >= DateTime.UtcNow.AddHours(-s.DuplicateWindowHours)),
	severity: s.DuplicatePolicy == 2 ? RuleSeverity.Block : RuleSeverity.Warn
);
		}
	}
}
