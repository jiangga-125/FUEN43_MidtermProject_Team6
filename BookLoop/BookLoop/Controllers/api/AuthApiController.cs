using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using BookLoop.Services;

namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/auth")]
	public class AuthApiController : ControllerBase
	{
		public record LoginDto(string Account, string Password);

		[HttpPost("login")]
		[AllowAnonymous]
		public async Task<IActionResult> Login(
			[FromBody] LoginDto dto,
			[FromServices] AuthService auth 
		)
		{
			var user = await auth.FindByEmailAsync(dto.Account);
			if (user == null)
				return Unauthorized(new { message = "帳號不存在" });

			if (!auth.VerifyPassword(user, dto.Password))
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			await auth.SignInAsync(user, isPersistent: true); // 發與 MVC 相同的 Cookie
			return Ok(new { message = "OK" });
		}

		[HttpGet("me")]
		public IActionResult Me()
		{
			if (!(User?.Identity?.IsAuthenticated ?? false))
				return Unauthorized(new { message = "未登入" });

			var userId = User.FindFirst("uid")?.Value
						 ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			var email = User.FindFirst(ClaimTypes.Email)?.Value;
			var name = User.Identity?.Name
					   ?? User.FindFirst(ClaimTypes.GivenName)?.Value
					   ?? User.FindFirst(ClaimTypes.Name)?.Value;

			var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
			var permissions = User.FindAll("perm").Select(c => c.Value).ToList();

			return Ok(new { user = new { userId, name, email }, roles, permissions });
		}

		[HttpPost("logout")]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Ok(new { message = "signed out" });
		}
	}
}
