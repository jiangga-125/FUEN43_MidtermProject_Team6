using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using BookLoop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

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
		// [FromServices] AppDbContext db 參數，用來存 Refresh Token
		public async Task<IActionResult> Token([FromBody] LoginDto dto, [FromServices] AuthService auth, [FromServices] AppDbContext db, [FromServices] IConfiguration cfg)
		{
			// 驗證帳密
			var user = await auth.FindByEmailAsync(dto.Account);
			if (user == null)
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			if (!auth.VerifyPassword(user, dto.Password))
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			// 讀取 jwt 設定
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

			// 如果 AuthService 支援取得 roles / permissions，用以下程式碼加入 claims

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

			// Refresh Token 產生、存DB、設 HttpOnly Cookie 的流程
			// 產生 raw refresh token 並 hash 存 DB
			var rawRefresh = RefreshTokenHelper.GenerateRefreshTokenRaw();
			var tokenHash = RefreshTokenHelper.HashRefreshToken(rawRefresh);

			// 取得 user id
			int.TryParse(uid, out int userIdInt); // 若無法 parse，請改成對應型別處理
			var rt = new RefreshToken
			{
				UserID = userIdInt,
				TokenHash = tokenHash,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddDays(30),
				IsRevoked = false
			};
			db.RefreshTokens.Add(rt);
			await db.SaveChangesAsync();

			// 設 HttpOnly cookie（跨域 dev: SameSite=None, Secure=true）
			var cookieOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Expires = rt.ExpiresAt,
				Path = "/"
			};
			Response.Cookies.Append("refreshToken", rawRefresh, cookieOptions);

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

		#region refresh 方法
		// refresh endpoint，使用 HttpOnly cookie 裡的 refreshToken 換新 access token
		[HttpPost("refresh")]
		[AllowAnonymous]
		public async Task<IActionResult> Refresh([FromServices] AppDbContext db, [FromServices] AuthService auth, [FromServices] IConfiguration cfg)
		{
			// 取 cookie
			if (!Request.Cookies.TryGetValue("refreshToken", out var rawToken))
				return Unauthorized(new { message = "refreshToken cookie not found" });

			// 計算 hash 並查 DB
			var hash = RefreshTokenHelper.HashRefreshToken(rawToken);
			var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash && !r.IsRevoked);
			if (existing == null || existing.ExpiresAt < DateTime.UtcNow)
				return Unauthorized(new { message = "invalid or expired refresh token" });

			// revoke old
			existing.IsRevoked = true;

			// rotate -> 新增一筆 refresh token
			var newRaw = RefreshTokenHelper.GenerateRefreshTokenRaw();
			var newHash = RefreshTokenHelper.HashRefreshToken(newRaw);
			var newRt = new RefreshToken
			{
				UserID = existing.UserID,
				TokenHash = newHash,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddDays(30),
				IsRevoked = false
			};
			db.RefreshTokens.Add(newRt);
			await db.SaveChangesAsync();

			// 設新的 HttpOnly cookie（覆蓋）
			Response.Cookies.Append("refreshToken", newRaw, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Expires = newRt.ExpiresAt,
				Path = "/"
			});

			// 產生新 access token（找 user -> 用原本的 token 產生邏輯）
			var user = await auth.FindByIdAsync(existing.UserID.ToString());
			if (user == null) return Unauthorized(new { message = "user not found" });

			// 產生 access token（複製 token 產生邏輯）
			var jwtSection = cfg.GetSection("Jwt");
			var keyStr = jwtSection["Key"] ?? throw new Exception("Jwt:Key 未設定");
			var issuer = jwtSection["Issuer"];
			var audience = jwtSection["Audience"];
			var accessMinutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "15");

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

			string uid = user?.GetType().GetProperty("UserId")?.GetValue(user)?.ToString()
				?? user?.GetType().GetProperty("Id")?.GetValue(user)?.ToString() ?? "";
			string email = user?.GetType().GetProperty("Email")?.GetValue(user)?.ToString() ?? "";
			string name = user?.GetType().GetProperty("UserName")?.GetValue(user)?.ToString() ?? email;

			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, email),
				new Claim("uid", uid),
				new Claim(ClaimTypes.Email, email),
				new Claim(ClaimTypes.Name, name),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

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
			// 回傳新 access token
			return Ok(new { token = tokenStr, expires = jwt.ValidTo });
		}
		#endregion

		#region logout方法
		[HttpPost("logout")]
		public async Task<IActionResult> Logout([FromServices] AppDbContext db)
		{
			// 撤銷 refreshToken（若有）
			if (Request.Cookies.TryGetValue("refreshToken", out var rawToken))
			{
				var hash = RefreshTokenHelper.HashRefreshToken(rawToken);
				var rt = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && !t.IsRevoked);
				if (rt != null)
				{
					rt.IsRevoked = true;
					await db.SaveChangesAsync();
				}
			}

			// 刪除 cookie
			Response.Cookies.Delete("refreshToken", new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Path = "/"
			});

			// signout cookie 行為保留
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Ok(new { message = "signed out" });
		}
		#endregion
	}
}
