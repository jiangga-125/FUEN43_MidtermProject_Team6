using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 會員.Models;
using System.Linq;
using 會員.Areas.Reviews;
using 會員.Models.ViewModels;


[ApiController]
[Route("api/reviews")]
public class ReviewsApiController : ControllerBase
{
	private readonly MemberContext _db;
	private readonly IReviewModerationService _mod;   // ★ 注入
	public ReviewsApiController(MemberContext db, IReviewModerationService mod)
	{ _db = db; _mod = mod; }

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateReviewVm vm)
	{
		if (!ModelState.IsValid) return BadRequest(ModelState);

		var e = new Review
		{
			MemberId = vm.MemberID,
			TargetType = vm.TargetType,
			TargetId = vm.TargetID,
			Rating = vm.Rating,
			Content = vm.Content,
			ImageUrls = null,
			Status = 0, // Pending
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		_db.Reviews.Add(e);
		await _db.SaveChangesAsync(); // 先存得到 ReviewID

		// ★ 跑自動審核 + 寫 Moderations + 更新 Reviews.Status
		var (ok, msg, decision) = await _mod.AutoModerateAndPersistAsync(e.ReviewId);
		if (!ok) return BadRequest(new { ok, message = msg });

		// ★ 取出最新的審核紀錄（拿 Note 當作理由）
		var mod = await _db.ReviewModerations
			.Where(m => m.ReviewId == e.ReviewId)
			.OrderByDescending(m => m.ModerationId)
			.Select(m => new { m.Decision, m.Reasons, m.ReviewedAt })
			.FirstOrDefaultAsync();

		return Ok(new
		{
			ok = true,
			reviewId = e.ReviewId,
			decision = decision.ToString(), // AutoPass / NeedsManual / Rejected
			status = e.Status,              // 1=Approved,0=Pending,2=Rejected
			reason = mod?.Reasons              // 例如：包含禁用字詞：白癡
		});
	}
}
