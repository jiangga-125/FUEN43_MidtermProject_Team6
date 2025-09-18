// Services/Reviews/Rules/ExtraRules.cs
using BookLoop.Areas.Reviews.Rules;

public class RatingRangeRule : IReviewRule
{
	public string Name => "RatingRangeRule";
	private readonly byte _min, _max;
	public RatingRangeRule(byte min, byte max) { _min = min; _max = max; }
	public RuleResult Evaluate(ReviewContext ctx)
	{
		var r = new RuleResult();
		if (ctx.Rating < _min || ctx.Rating > _max)
		{
			r.IsPass = false;
			r.Findings.Add(new RuleFinding(Name, RuleSeverity.Block, $"評分需介於{_min}~{_max}"));
		}
		return r;
	}
}

public class NoSelfReviewRule : IReviewRule
{
	public string Name => "NoSelfReviewRule";
	private readonly byte _ttMember;
	public NoSelfReviewRule(byte ttMember) { _ttMember = ttMember; }
	public RuleResult Evaluate(ReviewContext ctx)
	{
		var r = new RuleResult();
		if (ctx.TargetType == _ttMember && ctx.MemberID == ctx.TargetID)
		{
			r.IsPass = false;
			r.Findings.Add(new RuleFinding(Name, RuleSeverity.Block, "禁止自評"));
		}
		return r;
	}
}

// 讓重複規則可指定嚴重度（Warn or Block）
public class RepeatedContentRule : IReviewRule
{
	public string Name => "RepeatedContentRule";
	private readonly Func<int, string, bool> _isDuplicate;
	private readonly RuleSeverity _severity;
	public RepeatedContentRule(Func<int, string, bool> check, RuleSeverity severity)
	{
		_isDuplicate = check; _severity = severity;
	}
	public RuleResult Evaluate(ReviewContext ctx)
	{
		var r = new RuleResult();
		if (_isDuplicate(ctx.MemberID, (ctx.Content ?? string.Empty).Trim()))
		{
			if (_severity == RuleSeverity.Block) r.IsPass = false;
			r.Findings.Add(new RuleFinding(
				Name, _severity,
				_severity == RuleSeverity.Block ? "重複評論(阻擋)" : "重複評論(送人工)"));
		}
		return r;
	}
}
