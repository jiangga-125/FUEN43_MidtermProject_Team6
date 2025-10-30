// Controllers/Api/MembersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Security.Claims;
using BookLoop.Data;

namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize] // 需要驗證（JWT 或 cookie，取決於 Program.cs 的設定）
	public class MembersController : ControllerBase
	{
		private readonly MemberContext _db; // 或 MemberContext 如果你有獨立 MemberContext
		public MembersController(MemberContext db) { _db = db; }

		// GET: /api/members/me
		[HttpGet("me")]
		public async Task<IActionResult> Me()
		{
			// 嘗試從 claim 取得 user id（你的 token 或 auth 可能設定不同）
			// 如果你的 token 有 claim "uid" (int) 就用它；否則試 email(sub)
			var uidClaim = User.FindFirst("uid")?.Value;
			int? userId = null;
			if (!string.IsNullOrEmpty(uidClaim) && int.TryParse(uidClaim, out var parsed)) userId = parsed;

			if (userId == null)
			{
				// fallback: 用 email (sub) 去找 member 的 Email 欄位
				var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("sub")?.Value;
				if (string.IsNullOrEmpty(email)) return Forbid(); // 找不到身份
				var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);
				if (member == null) return NotFound();
				return Ok(new
				{
					id = member.MemberID,
					username = member.Username,
					email = member.Email,
					phone = member.Phone,
					role = member.Role,
					status = member.Status
				});
			}
			else
			{
				var member = await _db.Members.FirstOrDefaultAsync(m => m.MemberID == userId.Value);
				if (member == null) return NotFound();
				return Ok(new
				{
					id = member.MemberID,
					username = member.Username,
					email = member.Email,
					phone = member.Phone,
					role = member.Role,
					status = member.Status
				});
			}
		}
	}
}
