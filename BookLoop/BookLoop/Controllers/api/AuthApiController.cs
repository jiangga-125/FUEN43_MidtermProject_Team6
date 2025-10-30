using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

using BookLoop.Services;

namespace BookLoop.Controllers.Api
{
	/// <summary>
	/// 給 Vue 前端用的 Auth API（回 JSON）。不影響原本 MVC 的 AuthController。
	/// </summary>
	[ApiController]
	[Route("api/auth")]
	public class AuthApiController : ControllerBase
	{
		private readonly AuthService _auth;

		//public AuthApiController(AuthService auth)
		//{
		//	_auth = auth;
		//}
		public AuthApiController() { }
		public record LoginDto(string Account, string Password);

		/// <summary>
		/// 登入：帳密正確就簽發「與 MVC 相同」的 Cookie
		/// </summary>
		[HttpPost("login")]
		[AllowAnonymous]
		public async Task<IActionResult> Login([FromBody] LoginDto dto)
		{
			var user = await _auth.FindByEmailAsync(dto.Account);
			if (user == null) return Unauthorized(new { message = "帳號不存在" });

			if (!_auth.VerifyPassword(user, dto.Password))
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			await _auth.SignInAsync(user, isPersistent: true); // 與 MVC 相同簽 cookie
			return Ok(new { message = "OK" });
		}

		/// <summary>
		/// 取得目前登入者（從 Cookie Claims 直接讀）
		/// </summary>
		[HttpGet("me")]
		public IActionResult Me()
		{
			if (!User.Identity?.IsAuthenticated ?? true)
				return Unauthorized(new { message = "未登入" });

			var userId = User.FindFirst("uid")?.Value
						 ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			var email = User.FindFirst(ClaimTypes.Email)?.Value;
			var name = User.Identity?.Name
					   ?? User.FindFirst(ClaimTypes.GivenName)?.Value
					   ?? User.FindFirst(ClaimTypes.Name)?.Value;

			var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
			var permissions = User.FindAll("perm").Select(c => c.Value).ToList();

			return Ok(new
			{
				user = new { userId, name, email },
				roles,
				permissions
			});
		}

		/// <summary>
		/// 登出：清掉 Cookie
		/// </summary>
		[HttpPost("logout")]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Ok(new { message = "signed out" });
		}
	}
}
