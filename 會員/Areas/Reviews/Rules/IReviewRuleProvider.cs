// Services/Reviews/Rules/IReviewRuleProvider.cs
using 會員.Areas.Reviews.Rules;

public interface IReviewRuleProvider
{
	IEnumerable<IReviewRule> GetRules();
}
