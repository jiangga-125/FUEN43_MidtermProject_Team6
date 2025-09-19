// Services/Reviews/ReviewRulePipeline.cs
using System.Collections.Generic;
using System.Linq;
using BookLoop.Services.Rules;

namespace BookLoop.Areas.Reviews
{
	public interface IReviewRulePipeline
	{
		PipelineResult Run(ReviewContext ctx);
		string Version { get; } // 方便存快照
	}

	public class ReviewRulePipeline : IReviewRulePipeline
	{
		private readonly List<IReviewRule> _rules;
		public string Version { get; } = "v1.0.0"; // 變更規則時要更新

		public ReviewRulePipeline(IEnumerable<IReviewRule> rules)
		{
			_rules = rules.ToList();
		}

		public PipelineResult Run(ReviewContext ctx)
		{
			var findings = new List<RuleFinding>();
			bool anyBlock = false;
			bool anyWarn = false;

			foreach (var rule in _rules)
			{
				var res = rule.Evaluate(ctx);
				if (!res.IsPass) anyBlock = true;
				findings.AddRange(res.Findings);
				if (res.Findings.Any(f => f.Severity == RuleSeverity.Block)) anyBlock = true;
				if (res.Findings.Any(f => f.Severity == RuleSeverity.Warn)) anyWarn = true;
			}

			var result = new PipelineResult
			{
				Decision = anyBlock ? AutoDecision.Rejected :
						   anyWarn ? AutoDecision.NeedsManual :
									 AutoDecision.AutoPass,
				RuleVersionSnapshot = $"{Version}|{string.Join(",", _rules.Select(r => r.Name))}"
			};
			result.Findings.AddRange(findings);
			return result;
		}
	}
}
