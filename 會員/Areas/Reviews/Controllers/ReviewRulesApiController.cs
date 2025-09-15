using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 會員.Models;

[ApiController]
[Route("api/review-rules")]                // 不會和現有 RulesController 衝突
public class ReviewRulesApiController : ControllerBase
{
	private readonly MemberContext _db;
	public ReviewRulesApiController(MemberContext db) => _db = db;

	[HttpGet]                                // GET /api/review-rules
	public async Task<ActionResult<ReviewRuleSettings>> Get()
		=> await _db.ReviewRuleSettings.AsNoTracking().FirstAsync(x => x.Id == 1);

	public class ReviewRuleSettingDto
	{
		public int MinContentLength { get; set; } = 12;
		public byte RatingMin { get; set; } = 1;
		public byte RatingMax { get; set; } = 5;
		public bool BlockSelfReview { get; set; } = true;
		public byte TargetTypeForMember { get; set; } = 2;
		public bool ForbidUrls { get; set; } = true;
		public int DuplicateWindowHours { get; set; } = 24;
		public byte DuplicatePolicy { get; set; } = 1; // 0忽略 1警告 2阻擋
		public string? ForbiddenKeywords { get; set; }  // 逗號或換行
	}

	[HttpPut]                                // PUT /api/review-rules
	public async Task<IActionResult> Update([FromBody] ReviewRuleSettingDto dto)
	{
		var s = await _db.ReviewRuleSettings.FirstAsync(x => x.Id == 1);
		s.MinContentLength = dto.MinContentLength;
		s.RatingMin = dto.RatingMin;
		s.RatingMax = dto.RatingMax;
		s.BlockSelfReview = dto.BlockSelfReview;
		s.TargetTypeForMember = dto.TargetTypeForMember;
		s.ForbidUrls = dto.ForbidUrls;
		s.DuplicateWindowHours = dto.DuplicateWindowHours;
		s.DuplicatePolicy = dto.DuplicatePolicy;
		s.ForbiddenKeywords = dto.ForbiddenKeywords;
		s.UpdatedAt = DateTime.UtcNow;
		s.UpdatedBy = null; // 有權限系統時填入 AdminId
		await _db.SaveChangesAsync();
		return NoContent();
	}


}
