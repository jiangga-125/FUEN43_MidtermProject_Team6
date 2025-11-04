using BookLoop;
using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BookLoop.Controllers.Api
{
	[ApiController]
	[Route("api/auth")]
	[Produces("application/json")]
	public class AuthApiController : ControllerBase
	{
		private readonly AppDbContext _db;
		private readonly IConfiguration _cfg;
		private readonly IMemoryCache _cache;
		private readonly IWebHostEnvironment _env;

		public AuthApiController(AppDbContext db, IConfiguration cfg, IMemoryCache cache, IWebHostEnvironment env)
		{
			_db = db;
			_cfg = cfg;
			_cache = cache;
			_env = env;
		}

		// ==== DTOs ====
		public record LoginDto(string Account, string Password);

		// Email OTP（共用）
		public record EmailOtpDto(string Account, string Code);
		public record EmailSendDto(string Account, string? Purpose);

		// 註冊
		public record RegisterSimpleDto(string Email, string Password, string? Username);
		public record RegisterWithCodeDto(string Account, string Name, string Password, string Code);

		// 忘記/重設
		public record ForgotDto(string Email);
		public record ResetDto(string Email, string Code, string NewPassword);
		public record ResetConfirmDto(string Account, string Code, string Password);

		// TOTP
		public record TotpBindDto(string Account);
		public record TotpVerifyDto(string Account, string Code);

		// ==== 密碼雜湊（PBKDF2） ====
		private static string HashPasswordPbkdf2(string password, int iterations = 100_000, int saltSize = 16, int keySize = 32)
		{
			if (string.IsNullOrEmpty(password))
				throw new ArgumentException("password is required", nameof(password));

			var salt = RandomNumberGenerator.GetBytes(saltSize);
			using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
			var key = pbkdf2.GetBytes(keySize);
			return $"PBKDF2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
		}

		// ==== JWT / Claims 共用 ====
		private SymmetricSecurityKey BuildSigningKey()
		{
			var keyStr = _cfg.GetSection("Jwt")["Key"]
				?? throw new InvalidOperationException("Jwt:Key 未設定");
			try { return new SymmetricSecurityKey(Convert.FromBase64String(keyStr)); }
			catch { return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr)); }
		}

		private (string token, DateTime expiresUtc) IssueAccessToken(IEnumerable<Claim> claims)
		{
			var jwtSection = _cfg.GetSection("Jwt");
			var issuer = jwtSection["Issuer"];
			var audience = jwtSection["Audience"];
			var accessMinutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "15");

			var creds = new SigningCredentials(BuildSigningKey(), SecurityAlgorithms.HmacSha256);
			var now = DateTime.UtcNow;
			var jwt = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				notBefore: now,
				expires: now.AddMinutes(accessMinutes),
				signingCredentials: creds
			);
			return (new JwtSecurityTokenHandler().WriteToken(jwt), jwt.ValidTo);
		}

		private static List<Claim> BuildMemberClaims(Member m) => new()
		{
			new Claim(JwtRegisteredClaimNames.Sub, m.Email ?? m.Username),
			new Claim("mid", m.MemberID.ToString()),
			new Claim(ClaimTypes.Email, m.Email ?? string.Empty),
			new Claim(ClaimTypes.Name, m.Username),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
		};

		private static bool VerifyPassword(Member member, string password)
		{
			var stored = member.PasswordHash ?? string.Empty;
			if (string.IsNullOrEmpty(stored)) return false;

			// BCrypt
			if (stored.StartsWith("$2"))
			{ try { return BCrypt.Net.BCrypt.Verify(password, stored); } catch { } }

			// PBKDF2: PBKDF2$<iter>$<saltBase64>$<hashBase64>
			if (stored.StartsWith("PBKDF2$", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					var parts = stored.Split('$');
					int iterations = int.Parse(parts[1]);
					var salt = Convert.FromBase64String(parts[2]);
					var hash = Convert.FromBase64String(parts[3]);
					using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
					var test = pbkdf2.GetBytes(hash.Length);
					return CryptographicOperations.FixedTimeEquals(hash, test);
				}
				catch { }
			}

			// SHA256 hex
			if (stored.Length == 64 && stored.All(c => "0123456789abcdefABCDEF".Contains(c)))
			{
				var sha = SHA256.HashData(Encoding.UTF8.GetBytes(password));
				var hex = string.Concat(sha.Select(b => b.ToString("x2")));
				return string.Equals(stored, hex, StringComparison.OrdinalIgnoreCase);
			}

			// 退場機制：明碼（僅開發）
			return stored == password;
		}

		private static string NormalizeEmail(string? email)
			=> string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToUpperInvariant();

		private async Task SetRefreshCookieAsync(int memberId)
		{
			var rawRefresh = RefreshTokenHelper.GenerateRefreshTokenRaw();
			var tokenHash = RefreshTokenHelper.HashRefreshToken(rawRefresh);
			var days = int.Parse(_cfg["Jwt:RefreshTokenDays"] ?? "30");

			_db.MemberRefreshTokens.Add(new MemberRefreshToken
			{
				MemberId = memberId,
				TokenHash = tokenHash,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddDays(days),
				IsRevoked = false
			});
			await _db.SaveChangesAsync();

			Response.Cookies.Append("refreshToken", rawRefresh, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Path = "/",
				Expires = DateTime.UtcNow.AddDays(days)
			});
		}

		// ==================== 1) 帳密登入 ====================
		[HttpPost("token")]
		[AllowAnonymous]
		public async Task<IActionResult> Token([FromBody] LoginDto dto)
		{
			if (dto is null || string.IsNullOrWhiteSpace(dto.Account) || string.IsNullOrWhiteSpace(dto.Password))
				return BadRequest(new { message = "缺少帳號或密碼" });

			var accountNorm = NormalizeEmail(dto.Account);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == accountNorm || m.Username == dto.Account);
			if (member == null || !VerifyPassword(member, dto.Password))
				return Unauthorized(new { message = "帳號或密碼錯誤" });

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			await SetRefreshCookieAsync(member.MemberID);

			return Ok(new
			{
				token = tokenStr,
				expires = expiresUtc,
				member = new { memberId = member.MemberID, name = member.Username, email = member.Email }
			});
		}

		// ==================== 2) 我是誰（JWT） ====================
		[HttpGet("me")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public IActionResult Me()
		{
			if (!(User?.Identity?.IsAuthenticated ?? false))
				return Unauthorized(new { message = "未登入" });

			var memberId = User.FindFirst("mid")?.Value;
			var email = User.FindFirst(ClaimTypes.Email)?.Value;
			var name = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value;

			return Ok(new { member = new { memberId, name, email } });
		}

		// ==================== 3) Refresh（匿名） ====================
		[HttpPost("refresh")]
		[AllowAnonymous]
		public async Task<IActionResult> Refresh()
		{
			if (!Request.Cookies.TryGetValue("refreshToken", out var rawToken))
				return Unauthorized(new { message = "refreshToken cookie not found" });

			var hash = RefreshTokenHelper.HashRefreshToken(rawToken);
			var existing = await _db.MemberRefreshTokens
				.FirstOrDefaultAsync(r => r.TokenHash == hash && !r.IsRevoked);

			if (existing == null || existing.ExpiresAt < DateTime.UtcNow)
				return Unauthorized(new { message = "invalid or expired refresh token" });

			existing.IsRevoked = true;
			await _db.SaveChangesAsync();

			await SetRefreshCookieAsync(existing.MemberId);

			var member = await _db.Members.FirstOrDefaultAsync(m => m.MemberID == existing.MemberId);
			if (member == null) return Unauthorized(new { message = "member not found" });

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			return Ok(new { token = tokenStr, expires = expiresUtc });
		}

		// ==================== 4) 登出 ====================
		[HttpPost("logout")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public async Task<IActionResult> Logout()
		{
			if (Request.Cookies.TryGetValue("refreshToken", out var rawToken))
			{
				var hash = RefreshTokenHelper.HashRefreshToken(rawToken);
				var rt = await _db.MemberRefreshTokens
					.FirstOrDefaultAsync(t => t.TokenHash == hash && !t.IsRevoked);
				if (rt != null)
				{
					rt.IsRevoked = true;
					await _db.SaveChangesAsync();
				}
			}
			Response.Cookies.Delete("refreshToken", new CookieOptions
			{ HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Path = "/" });

			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Ok(new { message = "signed out" });
		}

		// ==================== 5) Email OTP：寄送 / 驗證 ====================

		// 統一寄送端點（Purpose: Register / ResetPassword）
		[HttpPost("email/send")]
		[AllowAnonymous]
		public async Task<IActionResult> EmailSend([FromBody] EmailSendDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Account))
				return BadRequest(new { message = "缺少帳號" });

			var email = dto.Account.Trim();
			if (!email.Contains("@") || !email.Contains("."))
				return BadRequest(new { message = "Email 格式不正確" });

			var norm = NormalizeEmail(email);
			var purpose = (dto.Purpose ?? "Register").Trim();

			// ResetPassword 需確認帳號存在；Register 不檢查
			if (purpose.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase))
			{
				var exists = await _db.Members.AnyAsync(m => m.EmailNormalized == norm);
				if (!exists) return NotFound(new { message = "帳號不存在" });
			}

			var code = Random.Shared.Next(100000, 999999).ToString();
			_cache.Set($"emailotp:{email}", code, TimeSpan.FromMinutes(10));

			if (_env.IsDevelopment()) return Ok(new { message = "OTP 已寄出", devCode = code });
			return Ok(new { message = "OTP 已寄出" });
		}

		// 舊路徑相容：/2fa/email/send → 轉呼叫新版 email/send（Purpose=Register）
		[HttpPost("2fa/email/send")]
		[AllowAnonymous]
		public Task<IActionResult> SendEmailOtp([FromBody] TotpBindDto dto)
			=> EmailSend(new EmailSendDto(dto?.Account ?? "", "Register"));

		// 用「Email OTP」直接登入（既有會員）
		[HttpPost("2fa/email/verify")]
		[AllowAnonymous]
		public async Task<IActionResult> VerifyEmailOtp([FromBody] EmailOtpDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Account) || string.IsNullOrWhiteSpace(dto.Code))
				return BadRequest(new { message = "缺少帳號或驗證碼" });

			if (!_cache.TryGetValue<string>($"emailotp:{dto.Account}", out var cached) || cached != dto.Code)
				return Unauthorized(new { message = "驗證碼錯誤或已過期" });

			var norm = NormalizeEmail(dto.Account);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			await SetRefreshCookieAsync(member.MemberID);

			_cache.Remove($"emailotp:{dto.Account}");
			return Ok(new
			{
				token = tokenStr,
				expires = expiresUtc,
				member = new { memberId = member.MemberID, name = member.Username, email = member.Email }
			});
		}

		// ==================== 6) TOTP 綁定 / 驗證 ====================
		[HttpPost("2fa/totp/bind")]
		[AllowAnonymous]
		public async Task<IActionResult> TotpBind([FromBody] TotpBindDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Account))
				return BadRequest(new { message = "缺少帳號" });

			var norm = NormalizeEmail(dto.Account);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			var secretBase32 = TotpHelper.GenerateSecretBase32();
			var issuer = "BookLoop";
			var otpauth = TotpHelper.GetOtpAuthUri(issuer, dto.Account, secretBase32);

			member.AuthenticatorKey = Encoding.UTF8.GetBytes(secretBase32);
			member.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			return Ok(new { secret = secretBase32, otpauth });
		}

		[HttpPost("2fa/totp/verify")]
		[AllowAnonymous]
		public async Task<IActionResult> TotpVerify([FromBody] TotpVerifyDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Account) || string.IsNullOrWhiteSpace(dto.Code))
				return BadRequest(new { message = "缺少帳號或驗證碼" });

			var norm = NormalizeEmail(dto.Account);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			if (member.AuthenticatorKey == null)
				return BadRequest(new { message = "尚未產生密鑰" });

			var secret = Encoding.UTF8.GetString(member.AuthenticatorKey);
			if (!TotpHelper.VerifyCode(secret, dto.Code, step: 30, window: 1))
				return Unauthorized(new { message = "TOTP 驗證失敗" });

			member.TwoFactorEnabled = true;
			member.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			return Ok(new { ok = true });
		}

		// ==================== 7) 註冊（兩種流派都支援） ====================

		// A) 簡單註冊（不驗碼）
		[HttpPost("register")]
		[AllowAnonymous]
		public async Task<IActionResult> RegisterSimple([FromBody] RegisterSimpleDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
				return BadRequest(new { message = "缺少 Email 或密碼" });

			var norm = NormalizeEmail(dto.Email);
			var exists = await _db.Members.AnyAsync(m => m.EmailNormalized == norm);
			if (exists) return Conflict(new { message = "Email 已存在" });

			var now = DateTime.UtcNow;
			var m = new Member
			{
				Username = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email : dto.Username!.Trim(),
				Email = dto.Email,
				EmailNormalized = norm,
				EmailConfirmed = true,
				PasswordHash = HashPasswordPbkdf2(dto.Password),
				Role = 0,
				Status = 1,
				CreatedAt = now,
				UpdatedAt = now,
				SecurityStamp = Guid.NewGuid().ToString("N"),
				TwoFactorEnabled = false,
				AccessFailedCount = 0,
				LockoutEnabled = true
			};
			_db.Members.Add(m);
			await _db.SaveChangesAsync();

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(m));
			await SetRefreshCookieAsync(m.MemberID);

			return Ok(new
			{
				token = tokenStr,
				expires = expiresUtc,
				member = new { memberId = m.MemberID, name = m.Username, email = m.Email }
			});
		}

		// B) 驗證碼註冊（需先 /api/auth/email/send Purpose=Register）
		[HttpPost("register/confirm")]
		[AllowAnonymous]
		public async Task<IActionResult> RegisterWithCode([FromBody] RegisterWithCodeDto dto)
		{
			if (dto == null) return BadRequest(new { message = "bad request" });
			var email = dto.Account?.Trim();
			if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "缺少 Email" });

			if (!_cache.TryGetValue<string>($"emailotp:{email}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "Email 驗證碼錯誤或已過期" });

			var norm = NormalizeEmail(email);
			var exists = await _db.Members.AnyAsync(x => x.EmailNormalized == norm);
			if (exists) return Conflict(new { message = "Email 已存在" });

			var now = DateTime.UtcNow;
			var m = new Member
			{
				Username = string.IsNullOrWhiteSpace(dto.Name) ? email! : dto.Name.Trim(),
				Email = email,
				EmailNormalized = norm,
				EmailConfirmed = true,
				PasswordHash = HashPasswordPbkdf2(dto.Password),
				Role = 0,
				Status = 1,
				CreatedAt = now,
				UpdatedAt = now,
				SecurityStamp = Guid.NewGuid().ToString("N"),
				TwoFactorEnabled = false,
				AccessFailedCount = 0,
				LockoutEnabled = true
			};
			_db.Members.Add(m);
			await _db.SaveChangesAsync();

			_cache.Remove($"emailotp:{email}");

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(m));
			await SetRefreshCookieAsync(m.MemberID);
			return Ok(new
			{
				token = tokenStr,
				expires = expiresUtc,
				member = new { memberId = m.MemberID, name = m.Username, email = m.Email }
			});
		}

		// ==================== 8) 忘記 / 重設密碼（同 /email/send + /reset/confirm 模式） ====================
		[HttpPost("forgot")]
		[AllowAnonymous]
		public async Task<IActionResult> Forgot([FromBody] ForgotDto dto)
		{
			// 這支你可保留（與 email/send(Purpose=ResetPassword) 作用等價）
			if (dto == null || string.IsNullOrWhiteSpace(dto.Email)) return BadRequest(new { message = "缺少 Email" });

			var norm = NormalizeEmail(dto.Email);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			var code = Random.Shared.Next(100000, 999999).ToString();
			_cache.Set($"reset:{dto.Email}", code, TimeSpan.FromMinutes(10));
			if (_env.IsDevelopment()) return Ok(new { message = "重設碼已寄出", devCode = code });
			return Ok(new { message = "重設碼已寄出" });
		}

		[HttpPost("reset")]
		[AllowAnonymous]
		public async Task<IActionResult> Reset([FromBody] ResetDto dto)
		{
			// 舊相容：用 reset:email 的碼
			if (dto == null ||
				string.IsNullOrWhiteSpace(dto.Email) ||
				string.IsNullOrWhiteSpace(dto.Code) ||
				string.IsNullOrWhiteSpace(dto.NewPassword))
				return BadRequest(new { message = "參數不完整" });

			if (!_cache.TryGetValue<string>($"reset:{dto.Email}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "重設碼錯誤或已過期" });

			var norm = NormalizeEmail(dto.Email);
			var m = await _db.Members.FirstOrDefaultAsync(x => x.EmailNormalized == norm);
			if (m == null) return BadRequest(new { message = "帳號不存在" });

			m.PasswordHash = HashPasswordPbkdf2(dto.NewPassword);
			m.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			_cache.Remove($"reset:{dto.Email}");
			return Ok(new { ok = true });
		}

		// 對齊新流程：/email/send(Purpose=ResetPassword) + /reset/confirm
		[HttpPost("reset/confirm")]
		[AllowAnonymous]
		public async Task<IActionResult> ResetConfirm([FromBody] ResetConfirmDto dto)
		{
			if (dto == null) return BadRequest(new { message = "bad request" });
			var email = dto.Account?.Trim();
			if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "缺少 Email" });

			if (!_cache.TryGetValue<string>($"emailotp:{email}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "驗證碼錯誤或已過期" });

			var norm = NormalizeEmail(email);
			var m = await _db.Members.FirstOrDefaultAsync(x => x.EmailNormalized == norm);
			if (m == null) return BadRequest(new { message = "帳號不存在" });

			m.PasswordHash = HashPasswordPbkdf2(dto.Password);
			m.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			_cache.Remove($"emailotp:{email}");
			return Ok(new { ok = true });
		}

		// ==================== 9) 外部登入 ====================
		[HttpGet("external/{provider}")]
		[AllowAnonymous]
		public IActionResult ExternalChallenge([FromRoute] string provider, [FromQuery] string? returnUrl = null)
		{
			var props = new AuthenticationProperties
			{
				RedirectUri = Url.Action(nameof(ExternalCallback), new { provider, returnUrl })
			};
			return Challenge(props, provider);
		}

		[HttpGet("external/{provider}/callback")]
		[AllowAnonymous]
		public async Task<IActionResult> ExternalCallback([FromRoute] string provider, [FromQuery] string? returnUrl = null)
		{
			var result = await HttpContext.AuthenticateAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);
			if (!result.Succeeded) return Unauthorized(new { message = "外部登入失敗" });

			var extId = result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			var email = result.Principal?.FindFirst(ClaimTypes.Email)?.Value;
			var name = result.Principal?.Identity?.Name ?? email ?? extId ?? "member";

			var accountKey = !string.IsNullOrEmpty(email) ? NormalizeEmail(email) : $"{provider}:{extId}".ToUpperInvariant();

			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == accountKey);
			if (member == null)
			{
				var now = DateTime.UtcNow;
				member = new Member
				{
					Username = email ?? $"{provider}_{extId}",
					Email = email,
					EmailNormalized = accountKey,
					EmailConfirmed = !string.IsNullOrEmpty(email),
					Role = 0,
					Status = 1,
					CreatedAt = now,
					UpdatedAt = now,
					SecurityStamp = Guid.NewGuid().ToString("N"),
					TwoFactorEnabled = false,
					AccessFailedCount = 0,
					LockoutEnabled = true
				};
				_db.Members.Add(member);
				await _db.SaveChangesAsync();
			}

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			await SetRefreshCookieAsync(member.MemberID);

			var dest = (returnUrl ?? "/") + $"#access_token={tokenStr}&expires={Uri.EscapeDataString(expiresUtc.ToString("o"))}";
			return Redirect(dest);
		}
	}
}
