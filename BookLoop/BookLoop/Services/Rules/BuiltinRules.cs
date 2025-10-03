// Services/Reviews/Rules/BuiltinRules.cs
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace BookLoop.Services.Rules
{
	// 1. 最小字數：太短容易灌水
	public class MinLengthRule : IReviewRule
	{
		public string Name => "MinLengthRule";
		private readonly int _min;

		public MinLengthRule(int min = 10) { _min = min; }

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			if (ctx.Content.Trim().Length < _min)
			{
				r.IsPass = false;
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Block, $"字數不足({_min}字起)"));
			}
			return r;
		}
	}

	// 2. 禁止 URL/聯絡方式（廣告風險）
	public class NoUrlOrContactRule : IReviewRule
	{
		public string Name => "NoUrlOrContactRule";
		private static readonly Regex UrlLike = new(@"https?://|www\.|line\s*id|＠|@|IG：|FB：|weixin|telegram", RegexOptions.IgnoreCase);

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			if (UrlLike.IsMatch(ctx.Content))
			{
				r.IsPass = false;
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Block, "疑似廣告/聯絡方式"));
			}
			return r;
		}
	}

	// 3. 敏感詞（可以擴充你的字典）
	public class ForbiddenKeywordsRule : IReviewRule
	{
		public string Name => "ForbiddenKeywordsRule";
		private readonly string[] _badWords;

		// ① 無參數：使用預設清單
		public ForbiddenKeywordsRule()
			: this(new[] { "幹", "媽的", "白癡", "智障" }) { }

		// ② 接受一條字串：逗號或換行分隔
		public ForbiddenKeywordsRule(string keywordsCsv)
			: this((keywordsCsv ?? "")
					.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim()))
		{ }

		// ③ 接受清單：已經拆好的就用這個
		public ForbiddenKeywordsRule(IEnumerable<string> keywords)
		{
			_badWords = (keywords ?? Array.Empty<string>())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			var text = ctx.Content ?? string.Empty;
			var hit = _badWords.FirstOrDefault(w =>
				text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
			if (hit != null)
			{
				r.IsPass = false;
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Block, $"包含不雅字詞：{hit}"));
			}
			return r;
		}
	}

	// 4. 重複內容（同會員 24h 內大量貼相同文字）
	public class RepeatedContentHintRule : IReviewRule
	{
		public string Name => "RepeatedContentHintRule";
		private readonly Func<int, string, bool> _isDuplicate; // 注入你的查詢方法

		public RepeatedContentHintRule(Func<int, string, bool> isDuplicate)
		{
			_isDuplicate = isDuplicate;
		}

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			if (_isDuplicate(ctx.MemberID, ctx.Content))
			{
				r.IsPass = true; // 不直接擋，先送人工
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Warn, "疑似重複評論"));
			}
			return r;
		}
	}

	// 5. 過度符號/全形表情/全大寫比例 -> 可疑灌水
	public class SpammyPatternRule : IReviewRule
	{
		public string Name => "SpammyPatternRule";

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			var content = ctx.Content;

			int exclamations = content.Count(c => c == '!' || c == '！');
			int emojis = content.Count(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherSymbol);

			if (exclamations >= 5 || emojis >= 10)
			{
				r.IsPass = true;
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Warn, "過多感嘆/表情符號"));
			}

			return r;
		}
	}

	// 6. 會員風險（新帳號/低信用分 → 送人工）
	public class MemberRiskRule : IReviewRule
	{
		public string Name => "MemberRiskRule";

		public RuleResult Evaluate(ReviewContext ctx)
		{
			var r = new RuleResult();
			if (ctx.MemberReputation <= -10 || ctx.MemberReviewCountLast24h >= 20)
			{
				r.IsPass = true;
				r.Findings.Add(new RuleFinding(Name, RuleSeverity.Warn, "會員風險較高，建議人工複審"));
			}
			return r;
		}
	}
}
