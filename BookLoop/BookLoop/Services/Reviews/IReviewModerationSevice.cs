using BookLoop.Services.Rules;

namespace BookLoop.Services.Reviews
{
	public interface IReviewModerationSevice
	{
		PipelineResult RunPipeline(ReviewContext ctx);
	}
}
