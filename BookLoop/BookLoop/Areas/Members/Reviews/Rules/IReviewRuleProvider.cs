// Services/Reviews/Rules/IReviewRuleProvider.cs
using BookLoop.Areas.Reviews.Rules;

public interface IReviewRuleProvider
{
	IEnumerable<IReviewRule> GetRules();
}
