// Services/Reviews/Rules/Contracts.cs
using System.Collections.Generic;

namespace 會員.Areas.Reviews.Rules
{
	// 審核輸入上下文（規則只讀）
	public class ReviewContext
	{
		public int MemberID { get; init; }
		public byte TargetType { get; init; }
		public int TargetID { get; init; }
		public byte Rating { get; init; }
		public string Content { get; init; } = "";
		public string? ImageUrls { get; init; }
		public int MemberReputation { get; init; } = 0; // 可接你的會員信用分系統(沒有就先0)
		public int MemberReviewCountLast24h { get; init; } = 0;
	}

	public enum RuleSeverity : byte
	{
		Info = 0,       // 提醒(不影響通過)
		Warn = 1,       // 警告(送人工)
		Block = 2       // 阻擋(直接拒絕)
	}

	public record RuleFinding(string RuleName, RuleSeverity Severity, string Message);

	public class RuleResult
	{
		public bool IsPass { get; set; } = true;                    // 此規則是否通過
		public List<RuleFinding> Findings { get; } = new();         // 累積此規則的理由
	}

	public interface IReviewRule
	{
		string Name { get; }
		RuleResult Evaluate(ReviewContext ctx);
	}

	// Pipeline 的最終決策
	public enum AutoDecision : byte
	{
		AutoPass = 0,        // 自動通過
		NeedsManual = 1,     // 送人工
		Rejected = 2         // 直接拒絕
	}

	public class PipelineResult
	{
		public AutoDecision Decision { get; set; }
		public List<RuleFinding> Findings { get; } = new();
		public string RuleVersionSnapshot { get; set; } = ""; // 方便存進 DB 追溯
	}
}
