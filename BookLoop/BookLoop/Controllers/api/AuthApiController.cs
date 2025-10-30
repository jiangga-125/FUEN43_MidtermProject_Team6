using BookLoop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/auth")]
	public class AuthApiController : ControllerBase
	{
		public record LoginDto(string Account, string Password);

		#region Login方法
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
		#endregion

		#region jwt token
		[HttpPost("token")]
		[AllowAnonymous]
		public async Task<IActionResult> Token([FromBody] LoginDto dto, [FromServices] AuthService auth)
		{
			// 驗證帳密
			var user = await auth.FindByEmailAsync(dto.Account);
			if (user == null)
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			if (!auth.VerifyPassword(user, dto.Password))
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			// 讀取 jwt 設定（請確認 appsettings 或 user-secrets 已設定）
			var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
			var jwtSection = cfg.GetSection("Jwt");
			var keyStr = jwtSection["Key"] ?? throw new Exception("Jwt:Key 未設定");
			var issuer = jwtSection["Issuer"];
			var audience = jwtSection["Audience"];
			var accessMinutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "15");

			// 建 signing key（支援 Base64 或 UTF8）
			SymmetricSecurityKey signingKey;
			try
			{
				var keyBytes = Convert.FromBase64String(keyStr);
				signingKey = new SymmetricSecurityKey(keyBytes);
			}
			catch
			{
				var keyBytes = Encoding.UTF8.GetBytes(keyStr);
				signingKey = new SymmetricSecurityKey(keyBytes);
			}

			// 取得 user 的基本欄位（用 reflection 容錯）
			string uid = user?.GetType().GetProperty("UserId")?.GetValue(user)?.ToString()
				?? user?.GetType().GetProperty("Id")?.GetValue(user)?.ToString() ?? "";
			string email = user?.GetType().GetProperty("Email")?.GetValue(user)?.ToString() ?? dto.Account;
			string name = user?.GetType().GetProperty("UserName")?.GetValue(user)?.ToString()
				?? user?.GetType().GetProperty("Name")?.GetValue(user)?.ToString() ?? email;

			// claims（按需擴充）
			var claims = new List<Claim>
			{
			new Claim(JwtRegisteredClaimNames.Sub, dto.Account),
			new Claim("uid", uid),
			new Claim(ClaimTypes.Email, email),
			new Claim(ClaimTypes.Name, name),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

			// **可選**：如果 AuthService 支援取得 roles / permissions，打開以下程式碼加入 claims
			/*
			var roles = await auth.GetRolesAsync(user); // 若有此方法
			foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

			var perms = await auth.GetPermissionsAsync(user); // 若有此方法
			foreach (var p in perms) claims.Add(new Claim("perm", p));
			*/

			var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
			var now = DateTime.UtcNow;
			var jwt = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				notBefore: now,
				expires: now.AddMinutes(accessMinutes),
				signingCredentials: creds
			);

			var tokenStr = new JwtSecurityTokenHandler().WriteToken(jwt);
			return Ok(new { token = tokenStr, expires = jwt.ValidTo });
		}
		#endregion

		#region me方法
		[HttpGet("me")]
		[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)] // 同時接受Cookie 與 JWT
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
		#endregion

		#region logout方法
		[HttpPost("logout")]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Ok(new { message = "signed out" });
		}
		#endregion
	}
}
