using BookLoop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting; // for IWebHostEnvironment
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

// 這些方法皆為開發用，僅在 Development 環境可用
[ApiController]
[Route("api/_debug")]
public class DevJwtController : ControllerBase
{
	// GET /api/_debug/devtoken?account=you@x.com&password=YourPw
	[HttpGet("devtoken")]
	[AllowAnonymous]
	public async Task<IActionResult> DevToken(
		[FromQuery] string account,
		[FromQuery] string password,
		[FromServices] AuthService auth,
		[FromServices] IConfiguration cfg,
		[FromServices] IWebHostEnvironment env)
	{
		// 只允許本機開發呼叫
		if (!env.IsDevelopment())
			return Forbid();

		if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
			return BadRequest(new { message = "請在 query 帶 account 與 password" });

		// 用你現有的 AuthService 驗證帳密
		var user = await auth.FindByEmailAsync(account);
		if (user == null || !auth.VerifyPassword(user, password))
			return Unauthorized(new { message = "帳號或密碼錯誤" });

		// 產 token（與 Program.cs 驗證邏輯一致）
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
		string email = user?.GetType().GetProperty("Email")?.GetValue(user)?.ToString() ?? account;
		string name = user?.GetType().GetProperty("UserName")?.GetValue(user)?.ToString() ?? email;

		var claims = new List<Claim>
		{
			new Claim(JwtRegisteredClaimNames.Sub, account),
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
		return Ok(new { token = tokenStr, expires = jwt.ValidTo });
	}

	// GET /api/_debug/validatetoken?token=eyJ...
	[HttpGet("validatetoken")]
	[AllowAnonymous]
	public IActionResult ValidateToken(
		[FromQuery] string token,
		[FromServices] IConfiguration cfg,
		[FromServices] IWebHostEnvironment env)
	{
		if (!env.IsDevelopment())
			return Forbid();

		if (string.IsNullOrEmpty(token))
			return BadRequest(new { message = "請在 query 帶 token" });

		var jwtSection = cfg.GetSection("Jwt");
		var keyStr = jwtSection["Key"] ?? throw new Exception("Jwt:Key 未設定");
		var issuer = jwtSection["Issuer"];
		var audience = jwtSection["Audience"];

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

		var tokenHandler = new JwtSecurityTokenHandler();
		try
		{
			var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidIssuer = issuer,
				ValidateAudience = true,
				ValidAudience = audience,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = signingKey,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.FromMinutes(2)
			}, out var validatedToken);

			// 回傳 claims 方便檢查
			var claims = principal.Claims.Select(c => new { c.Type, c.Value }).ToList();
			return Ok(new { ok = true, claims });
		}
		catch (Exception ex)
		{
			return BadRequest(new { ok = false, error = ex.Message });
		}
	}
}
