// Services/Reviews/Rules/IReviewRuleProvider.cs
using BookLoop.Services.Rules;

public interface IReviewRuleProvider
{
	IEnumerable<IReviewRule> GetRules();
}
