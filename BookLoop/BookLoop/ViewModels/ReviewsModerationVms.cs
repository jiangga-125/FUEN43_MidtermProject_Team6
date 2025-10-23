using BookLoop.Models;
using BookLoop.Services.Reviews;

namespace BookLoop.Models.ViewModels.Reviews
{
	public class PendingListVm
	{
		public IList<Review> Items { get; set; } = new List<Review>();
		public int Total { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int? TargetId { get; set; }
		public byte? TargetType { get; set; }
	}

	public class ReviewDetailVm
	{
		public Review Review { get; set; } = null!;
		public IList<ReviewRuleEngine.Finding> Findings { get; set; } = new List<ReviewRuleEngine.Finding>();
		public string RuleSnapshot { get; set; } = "{}";
	}

	public class RejectDto
	{
		public int ReviewId { get; set; }
		public int AdminId { get; set; }
		public string? Reason { get; set; }
	}
}
