using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/_debug")]
	public class DebugController : ControllerBase
	{
		// GET /api/_debug/ping  → 200 OK
		[HttpGet("ping")]
		[AllowAnonymous]
		public IActionResult Ping() => Ok(new { ok = true, time = DateTime.UtcNow });

		// GET /api/_debug/whoami → 看看目前是否登入（不會丟例外）
		[HttpGet("whoami")]
		[AllowAnonymous]
		public IActionResult WhoAmI()
		{
			var isAuth = User?.Identity?.IsAuthenticated == true;
			var name = User?.Identity?.Name;
			return Ok(new { isAuthenticated = isAuth, name });
		}
	}
}
