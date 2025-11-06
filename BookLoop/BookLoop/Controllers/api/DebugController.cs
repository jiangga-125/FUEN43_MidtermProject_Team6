using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/debug")]
	public class DebugController : ControllerBase
	{
		// GET /api/_debug/ping
		[HttpGet("ping")]
		[AllowAnonymous]
		public IActionResult Ping() => Ok(new { ok = true, time = DateTime.UtcNow });

		// GET /api/_debug/whoami
		[HttpGet("whoami")]
		[AllowAnonymous]
		public IActionResult WhoAmI()
		{
			var isAuth = User?.Identity?.IsAuthenticated == true;
			var name = User?.Identity?.Name;
			return Ok(new { isAuthenticated = isAuth, name });
		}

		[HttpGet("claims")]
		public IActionResult Claims()
		{
			var list = User?.Claims?
				.Select(c => new { c.Type, c.Value })
				.ToList() ?? new();
			return Ok(list);
		}
	}
}
