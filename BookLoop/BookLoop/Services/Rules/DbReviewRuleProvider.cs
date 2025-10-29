// Services/Reviews/Rules/DbReviewRuleProvider.cs
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;
using System.Collections.Generic;
using BookLoop.Data;
using BookLoop.Services.Rules;

public class DbReviewRuleProvider : IReviewRuleProvider
{
	private readonly MemberContext _db;

	public DbReviewRuleProvider(MemberContext db) => _db = db;

	public IEnumerable<IReviewRule> GetRules()
	{
		// 嘗試抓 Id=1，如果沒有就給一個預設
		var s = _db.ReviewRuleSettings.AsNoTracking().FirstOrDefault(x => x.Id == 1);
		if (s == null)
		{
			// 沒找到 → 給預設值，避免炸掉
			s = new ReviewRuleSettings
			{
				Id = 1,
				MinContentLength = 5,
				RatingMin = 1,
				RatingMax = 5,
				BlockSelfReview = false,
				TargetTypeForMember = 0,
				ForbidUrls = false,
				DuplicateWindowHours = 0,
				DuplicatePolicy = 0,
				ForbiddenKeywords = "",
				UpdatedAt = DateTime.UtcNow
			};
		}
		//新增防呆
		//// 讀單一設定（Id=1）
		//var s = _db.ReviewRuleSettings.AsNoTracking().First(x => x.Id == 1);

		// 1) 字數
		yield return new MinLengthRule(s.MinContentLength);

		// 2) 評分範圍（用資料庫 CHECK 已擋，但也可在規則層加保險）
		yield return new RatingRangeRule(s.RatingMin, s.RatingMax);

		// 3) 禁網址/聯絡方式
		if (s.ForbidUrls) yield return new NoUrlOrContactRule();

		// 4) 禁自評（只在評會員時檢查）
		if (s.BlockSelfReview) yield return new NoSelfReviewRule(s.TargetTypeForMember);

		// 5) 敏感詞（從資料庫 ReviewForbiddenKeyword 讀取）
		var hasDbKeywords = _db.ReviewForbiddenKeyword.Any(k => k.IsActive);
		if (hasDbKeywords)
		{
			yield return new ForbiddenKeywordsRule(_db); // ✅ 使用新版規則（從資料庫撈）
		}
		else if (!string.IsNullOrWhiteSpace(s.ForbiddenKeywords))
		{
			yield return new ForbiddenKeywordsRule(s.ForbiddenKeywords); // ✅ 備用：從設定表載入
		}
		else
		{
			yield return new ForbiddenKeywordsRule(); // ✅ 備用：用預設字詞
		}



		// 6) 重複偵測（Warn 或 Block 由設定決定）
		if (s.DuplicatePolicy != 0)
		{
			var severity = s.DuplicatePolicy == 2 ? RuleSeverity.Block : RuleSeverity.Warn;
			yield return new RepeatedContentRule(
			check: (authorId, content) =>
				_db.Reviews.Any(r =>
					r.MemberID == authorId &&
					r.Content == content.Trim() &&
					r.CreatedAt >= DateTime.UtcNow.AddHours(-s.DuplicateWindowHours)),
			severity: s.DuplicatePolicy == 2 ? RuleSeverity.Block : RuleSeverity.Warn
		);
		}
	}
}
