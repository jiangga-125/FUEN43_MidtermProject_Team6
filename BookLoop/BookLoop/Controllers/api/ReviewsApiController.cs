using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using BookLoop.Models;
using BookLoop.Data;
using BookLoop.Models.ViewModels;
using BookLoop.Areas.Reviews;

namespace BookLoop.Controllers.api;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]/[action]")]
public class ReviewsApiController : ControllerBase
{
	private readonly MemberContext _db;
	private readonly IReviewModerationService _mod;

	public ReviewsApiController(MemberContext db, IReviewModerationService mod)
	{
		_db = db;
		_mod = mod;
	}

	// 🔑 從 JWT Token Claims 中取得目前登入會員 ID
	private int? GetMemberIdFromClaims()
	{
		var idClaim = User.FindFirst("memberId")
				   ?? User.FindFirst(ClaimTypes.NameIdentifier)
				   ?? User.FindFirst("sub");
		if (idClaim == null) return null;
		return int.TryParse(idClaim.Value, out var id) ? id : null;
	}

	// ✅ 建立評論（自動綁定登入會員）
	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateReviewVm vm)
	{
		if (!ModelState.IsValid)
			return BadRequest(ModelState);

		var memberId = GetMemberIdFromClaims();
		if (memberId == null)
			return Unauthorized(new { success = false, message = "missing_memberId_in_token" });

		var e = new Review
		{
			MemberID = memberId.Value,              // ✅ 自動帶入會員ID
			TargetType = (byte)ReviewTargetType.Book,
			// 固定為書籍評論
			TargetID = vm.TargetBookId,
			Rating = vm.Rating,
			Content = vm.Content,
			ImageUrls = vm.TargetBookName,          // 若有封面或圖片連結
			Status = 0,                             // 0 = Pending
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow

		};

		_db.Reviews.Add(e);
		await _db.SaveChangesAsync(); // 拿到 ReviewID

		// 🧠 執行自動審核流程
		var (ok, msg, decision) = await _mod.AutoModerateAndPersistAsync(e.ReviewID);
		if (!ok) return BadRequest(new { ok, message = msg });

		// 🧾 取出最新的審核紀錄
		var mod = await _db.ReviewModerations
			.Where(m => m.ReviewID == e.ReviewID)
			.OrderByDescending(m => m.ModerationID)
			.Select(m => new { m.Decision, m.Reasons, m.ReviewedAt })
			.FirstOrDefaultAsync();

		return Ok(new
		{
			ok = true,
			reviewId = e.ReviewID,
			decision = decision.ToString(),  // AutoPass / NeedsManual / Rejected
			status = e.Status,               // 1=Approved, 0=Pending, 2=Rejected
			reason = mod?.Reasons            // 例如「包含禁用字詞：白癡」
		});
	}
}

public enum ReviewTargetType
{
    Book = 1,
    // 其他類型可依需求擴充
}
