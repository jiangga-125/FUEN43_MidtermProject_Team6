using System.Collections.Generic;
using System.Linq;
using BookLoop.Services.Rules;

namespace BookLoop.Areas.Reviews
{
	public interface IReviewRulePipeline
	{
		PipelineResult Run(ReviewContext ctx);
		string Version { get; }
	}

	public class ReviewRulePipeline : IReviewRulePipeline
	{
		private readonly IReviewRuleProvider _provider; // ✅ 改為從 provider 取得規則
		public string Version { get; } = "v1.0.0";

		public ReviewRulePipeline(IReviewRuleProvider provider)
		{
			_provider = provider;
		}

		public PipelineResult Run(ReviewContext ctx)
		{
			var findings = new List<RuleFinding>();
			bool anyBlock = false;
			bool anyWarn = false;

			// ✅ 這裡直接向 DbReviewRuleProvider 取得規則
			var rules = _provider.GetRules().ToList();

			// 除錯用，可看 Console Output
			Console.WriteLine("=== 已載入規則 ===");
			foreach (var r in rules) Console.WriteLine($"→ {r.Name}");

			foreach (var rule in rules)
			{
				var res = rule.Evaluate(ctx);

				findings.AddRange(res.Findings);

				if (res.Findings.Any(f => f.Severity == RuleSeverity.Block))
					anyBlock = true;
				if (res.Findings.Any(f => f.Severity == RuleSeverity.Warn))
					anyWarn = true;
			}

			var result = new PipelineResult
			{
				Decision = anyBlock ? AutoDecision.Rejected :
						   anyWarn ? AutoDecision.NeedsManual :
									 AutoDecision.AutoPass,
				RuleVersionSnapshot = $"{Version}|{string.Join(",", rules.Select(r => r.Name))}"
			};
			result.Findings.AddRange(findings);
			return result;
		}
	}
}
