// 路徑：BookLoop/Controllers/Api/AuthApiController.cs
using BookLoop;
using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using BookLoop.Services.Mail; // 使用 IMailService
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
		private readonly IMailService _mail;

		public AuthApiController(
			AppDbContext db,
			IConfiguration cfg,
			IMemoryCache cache,
			IWebHostEnvironment env,
			IMailService mail)
		{
			_db = db;
			_cfg = cfg;
			_cache = cache;
			_env = env;
			_mail = mail;
		}

		// ==== DTOs ====
		public record LoginDto(string Account, string Password);
		public record EmailOtpDto(
			string Account,
			string Code,
			bool? RememberDevice = null,
			string? DeviceHash = null
		);
		public record EmailSendDto(string Account, string? Purpose);
		public record RegisterSimpleDto(string Email, string Password, string? Username);
		public record RegisterWithCodeDto(string Account, string Name, string Password, string Code);
		public record ForgotDto(string Email);
		public record ResetDto(string Email, string Code, string NewPassword);
		public record ResetConfirmDto(string Account, string Code, string Password);
		public record TotpBindDto(string Account);
		public record TotpVerifyDto(string Account, string Code);

		// 用於保存 Google token（示範用快取；正式建議落 DB 並加密）
		private class GoogleTokenBundle
		{
			public string AccessToken { get; set; } = "";
			public string? RefreshToken { get; set; }
			public DateTimeOffset ExpiresAt { get; set; }
		}

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

			if (stored.StartsWith("$2"))
			{ try { return BCrypt.Net.BCrypt.Verify(password, stored); } catch { } }

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

			if (stored.Length == 64 && stored.All(c => "0123456789abcdefABCDEF".Contains(c)))
			{
				var sha = SHA256.HashData(Encoding.UTF8.GetBytes(password));
				var hex = string.Concat(sha.Select(b => b.ToString("x2")));
				return string.Equals(stored, hex, StringComparison.OrdinalIgnoreCase);
			}

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

			var isProd = !_env.IsDevelopment(); // 依你專案的準則決定
			Response.Cookies.Append("refreshToken", rawRefresh, new CookieOptions
			{
				HttpOnly = true,
				Secure = isProd,                 // 只有正式環境強制 Secure
				SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax, // 本機開發用 Lax 比較好測
				Path = "/",
				Expires = DateTime.UtcNow.AddDays(days)
			});
		}

		// 寄 OTP
		private Task SendOtpEmailAsync(string email, string purpose, string code, DateTimeOffset expiresAt)
		{
			var subject = purpose.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase)
				? "【BookLoop】重設密碼驗證碼"
				: "【BookLoop】Email 驗證碼";

			var html = $@"
<p>您好，</p>
<p>您的一次性驗證碼為：</p>
<h2 style=""letter-spacing:3px"">{code}</h2>
<p>有效期限：{expiresAt:yyyy/MM/dd HH:mm}</p>
<p>若非本人操作，請忽略本信。</p>";

			return _mail.SendAsync(
				to: email,
				subject: subject,
				body: html,
				attachmentName: null,
				attachmentBytes: null,
				contentType: "text/html",
				templateId: null,
				templateKey: purpose.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase) ? "Auth.ResetPassword.OTP" : "Auth.EmailVerification.OTP",
				templateVersionId: null,
				mailJobId: null,
				jobRecipientId: null,
				category: purpose.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase) ? "Auth/Reset" : "Auth/Register",
				cancellationToken: HttpContext.RequestAborted);
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

		// ==================== 3) Refresh ====================
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

			if (purpose.Equals("ResetPassword", StringComparison.OrdinalIgnoreCase))
			{
				var exists = await _db.Members.AnyAsync(m => m.EmailNormalized == norm);
				if (!exists) return NotFound(new { message = "帳號不存在" });
			}

			var throttleKey = $"emailotp:throttle:{norm}";
			if (_cache.TryGetValue(throttleKey, out _))
				return StatusCode(429, new { message = "寄送過於頻繁，請稍後再試" });
			_cache.Set(throttleKey, 1, TimeSpan.FromSeconds(60));

			var code = Random.Shared.Next(100000, 999999).ToString();
			var expires = DateTimeOffset.UtcNow.AddMinutes(10);

			_cache.Set($"emailotp:{norm}", code, TimeSpan.FromMinutes(10));

			await SendOtpEmailAsync(email, purpose, code, expires);

			if (_env.IsDevelopment())
				return Ok(new { message = "OTP 已寄出", devCode = code, expires });

			return Ok(new { message = "OTP 已寄出", expires });
		}

		[HttpPost("2fa/email/send")]
		[AllowAnonymous]
		public Task<IActionResult> SendEmailOtp([FromBody] TotpBindDto dto)
			=> EmailSend(new EmailSendDto(dto?.Account ?? "", "Register"));

		[HttpPost("2fa/email/verify")]
		[AllowAnonymous]
		public async Task<IActionResult> VerifyEmailOtp([FromBody] EmailOtpDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Account) || string.IsNullOrWhiteSpace(dto.Code))
				return BadRequest(new { message = "缺少帳號或驗證碼" });

			var norm = NormalizeEmail(dto.Account);
			if (!_cache.TryGetValue<string>($"emailotp:{norm}", out var cached) || cached != dto.Code)
				return Unauthorized(new { message = "驗證碼錯誤或已過期" });

			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			// ? 記住此裝置（可選）
			if ((dto.RememberDevice ?? false) && !string.IsNullOrWhiteSpace(dto.DeviceHash))
			{
				// 讀取保存天數（appsettings: Auth:EmailOtp:BypassIfTrustedDeviceDays；預設 30）
				int days = 30;
				if (int.TryParse(_cfg["Auth:EmailOtp:BypassIfTrustedDeviceDays"], out var cfgDays) && cfgDays > 0)
					days = cfgDays;

				var now = DateTime.UtcNow;
				var dev = await _db.MemberTrustedDevices
					.FirstOrDefaultAsync(x => x.MemberID == member.MemberID && x.DeviceHash == dto.DeviceHash);

				if (dev == null)
				{
					_db.MemberTrustedDevices.Add(new MemberTrustedDevice
					{
						MemberID = member.MemberID,
						DeviceHash = dto.DeviceHash!,
						DeviceName = "我的裝置",
						CreatedAt = now,
						LastUsedAt = now,
						ExpiresAtUtc = now.AddDays(days)
					});
				}
				else
				{
					dev.LastUsedAt = now;
					dev.ExpiresAtUtc = now.AddDays(days);
				}
				await _db.SaveChangesAsync();
			}

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			await SetRefreshCookieAsync(member.MemberID);

			_cache.Remove($"emailotp:{norm}");
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

		// ==================== 7) 註冊 ====================
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

		[HttpPost("register/confirm")]
		[AllowAnonymous]
		public async Task<IActionResult> RegisterWithCode([FromBody] RegisterWithCodeDto dto)
		{
			if (dto == null) return BadRequest(new { message = "bad request" });
			var email = dto.Account?.Trim();
			if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "缺少 Email" });

			var norm = NormalizeEmail(email);
			if (!_cache.TryGetValue<string>($"emailotp:{norm}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "Email 驗證碼錯誤或已過期" });

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

			_cache.Remove($"emailotp:{norm}");

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(m));
			await SetRefreshCookieAsync(m.MemberID);
			return Ok(new
			{
				token = tokenStr,
				expires = expiresUtc,
				member = new { memberId = m.MemberID, name = m.Username, email = m.Email }
			});
		}

		// ==================== 8) 忘記 / 重設密碼 ====================
		[HttpPost("forgot")]
		[AllowAnonymous]
		public async Task<IActionResult> Forgot([FromBody] ForgotDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.Email)) return BadRequest(new { message = "缺少 Email" });

			var email = dto.Email.Trim();
			var norm = NormalizeEmail(email);
			var member = await _db.Members.FirstOrDefaultAsync(m => m.EmailNormalized == norm);
			if (member == null) return NotFound(new { message = "帳號不存在" });

			var code = Random.Shared.Next(100000, 999999).ToString();
			var expires = DateTimeOffset.UtcNow.AddMinutes(10);

			_cache.Set($"emailotp:{norm}", code, TimeSpan.FromMinutes(10));

			await SendOtpEmailAsync(email, "ResetPassword", code, expires);

			if (_env.IsDevelopment())
				return Ok(new { message = "重設碼已寄出", devCode = code, expires });

			return Ok(new { message = "重設碼已寄出", expires });
		}

		[HttpPost("reset")]
		[AllowAnonymous]
		public async Task<IActionResult> Reset([FromBody] ResetDto dto)
		{
			if (dto == null ||
				string.IsNullOrWhiteSpace(dto.Email) ||
				string.IsNullOrWhiteSpace(dto.Code) ||
				string.IsNullOrWhiteSpace(dto.NewPassword))
				return BadRequest(new { message = "參數不完整" });

			var norm = NormalizeEmail(dto.Email);
			if (!_cache.TryGetValue<string>($"emailotp:{norm}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "重設碼錯誤或已過期" });

			var m = await _db.Members.FirstOrDefaultAsync(x => x.EmailNormalized == norm);
			if (m == null) return BadRequest(new { message = "帳號不存在" });

			m.PasswordHash = HashPasswordPbkdf2(dto.NewPassword);
			m.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			_cache.Remove($"emailotp:{norm}");
			return Ok(new { ok = true });
		}

		[HttpPost("reset/confirm")]
		[AllowAnonymous]
		public async Task<IActionResult> ResetConfirm([FromBody] ResetConfirmDto dto)
		{
			if (dto == null) return BadRequest(new { message = "bad request" });
			var email = dto.Account?.Trim();
			if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "缺少 Email" });

			var norm = NormalizeEmail(email);
			if (!_cache.TryGetValue<string>($"emailotp:{norm}", out var cached) || cached != dto.Code)
				return BadRequest(new { message = "驗證碼錯誤或已過期" });

			var m = await _db.Members.FirstOrDefaultAsync(x => x.EmailNormalized == norm);
			if (m == null) return BadRequest(new { message = "帳號不存在" });

			m.PasswordHash = HashPasswordPbkdf2(dto.Password);
			m.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			_cache.Remove($"emailotp:{norm}");
			return Ok(new { ok = true });
		}

		// ==================== 9) 外部登入（popup + postMessage） ====================
		[HttpGet("external/{provider}")]
		[AllowAnonymous]
		public IActionResult ExternalChallenge([FromRoute] string provider, [FromQuery] string? returnUrl = null)
		{
			if (!IsSafeReturnUrl(returnUrl)) returnUrl = "/";

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
			if (!result.Succeeded || result.Principal == null)
				return Content(HtmlCloseWithError("external_auth_failed", "外部登入失敗"));

			var extId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;

			var accessToken = result.Properties?.GetTokenValue("access_token");
			var idToken = result.Properties?.GetTokenValue("id_token");
			var refreshToken = result.Properties?.GetTokenValue("refresh_token");
			var expiresAtStr = result.Properties?.GetTokenValue("expires_at");

			DateTimeOffset googleExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
			if (!string.IsNullOrWhiteSpace(expiresAtStr) &&
				DateTimeOffset.TryParse(expiresAtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
			{
				googleExpiresAt = dto;
			}

			var accountKey = !string.IsNullOrWhiteSpace(email) ? NormalizeEmail(email)
							: $"{provider}:{extId}".ToUpperInvariant();

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

			if (!string.IsNullOrEmpty(accessToken))
			{
				var cacheKey = $"google:tokens:{member.MemberID}";
				_cache.Set(cacheKey, new GoogleTokenBundle
				{
					AccessToken = accessToken,
					RefreshToken = refreshToken,
					ExpiresAt = googleExpiresAt
				}, TimeSpan.FromHours(12));
			}

			var (tokenStr, expiresUtc) = IssueAccessToken(BuildMemberClaims(member));
			await SetRefreshCookieAsync(member.MemberID);

			await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

			var frontBase = _cfg["Frontend:BaseUrl"]?.TrimEnd('/') ?? "/";
			if (!IsSafeReturnUrl(returnUrl)) returnUrl = "/";
			var target = BuildFrontEndTarget(frontBase, returnUrl);

			return Content(
				HtmlCloseWithSuccess(provider, target, tokenStr, new DateTimeOffset(expiresUtc).ToUnixTimeSeconds()),
				"text/html; charset=utf-8"
			);
		}

		// ==================== 10) 後端代理呼叫 Google API（範例：userinfo） ====================
		[HttpGet("google/profile")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public async Task<IActionResult> GetGoogleProfile()
		{
			var midStr = User.FindFirst("mid")?.Value;
			if (string.IsNullOrWhiteSpace(midStr) || !int.TryParse(midStr, out var mid))
				return Unauthorized(new { message = "invalid member id" });

			var bundle = _cache.Get<GoogleTokenBundle>($"google:tokens:{mid}");
			if (bundle == null)
				return NotFound(new { message = "google tokens not found; please re-login with Google" });

			// 過期則嘗試用 refresh_token 換新
			if (bundle.ExpiresAt <= DateTimeOffset.UtcNow && !string.IsNullOrEmpty(bundle.RefreshToken))
			{
				var newBundle = await RefreshGoogleAccessTokenAsync(bundle.RefreshToken!);
				if (newBundle != null)
				{
					bundle.AccessToken = newBundle.AccessToken;
					bundle.ExpiresAt = newBundle.ExpiresAt;
					_cache.Set($"google:tokens:{mid}", bundle, TimeSpan.FromHours(12));
				}
			}

			using var client = new HttpClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bundle.AccessToken);

			var resp = await client.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
			if (!resp.IsSuccessStatusCode)
				return StatusCode((int)resp.StatusCode, new { message = "google api error" });

			var json = await resp.Content.ReadAsStringAsync();
			return Content(json, "application/json; charset=utf-8");
		}

		private async Task<GoogleTokenBundle?> RefreshGoogleAccessTokenAsync(string refreshToken)
		{
			var clientId = _cfg["Authentication:Google:ClientId"]!;
			var clientSecret = _cfg["Authentication:Google:ClientSecret"]!;

			using var client = new HttpClient();
			var form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = clientId,
				["client_secret"] = clientSecret,
				["grant_type"] = "refresh_token",
				["refresh_token"] = refreshToken
			});
			var resp = await client.PostAsync("https://oauth2.googleapis.com/token", form);
			if (!resp.IsSuccessStatusCode) return null;

			var json = await resp.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			var accessToken = root.GetProperty("access_token").GetString()!;
			var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

			return new GoogleTokenBundle
			{
				AccessToken = accessToken,
				RefreshToken = refreshToken,
				ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
			};
		}

		// ---- Private helpers ----
		private static bool IsSafeReturnUrl(string? url)
		{
			if (string.IsNullOrWhiteSpace(url)) return false;
			return url.StartsWith("/") && !url.StartsWith("//");
		}

		private static string BuildFrontEndTarget(string frontBase, string? returnUrl)
		{
			if (string.IsNullOrWhiteSpace(returnUrl) || !IsSafeReturnUrl(returnUrl))
				return frontBase;
			return frontBase + returnUrl;
		}

		// 回傳給 popup 的最小 HTML（成功）
		private string HtmlCloseWithSuccess(string provider, string targetUrl, string accessToken, long expiresAt) => $@"
<!doctype html><html><body>
<script>
  (function() {{
    try {{
      if (window.opener && window.opener !== window) {{
        window.opener.postMessage({{
          type: 'oauth-success',
          provider: '{provider.ToLowerInvariant()}',
          accessToken: '{accessToken}',
          expiresAt: {expiresAt}
        }}, '{targetUrl}');
      }}
    }} catch (e) {{ }}
    window.close();
  }})();
</script>
登入成功，視窗將自動關閉。
</body></html>";

		// 回傳給 popup 的最小 HTML（失敗）
		private string HtmlCloseWithError(string code, string message) => $@"
<!doctype html><html><body>
<script>
  (function() {{
    try {{
      if (window.opener && window.opener !== window) {{
        window.opener.postMessage({{
          type: 'oauth-error',
          provider: 'google',
          error: '{code}',
          message: '{message}'
        }}, '*');
      }}
    }} catch (e) {{ }}
    window.close();
  }})();
</script>
登入失敗：{message}
</body></html>";
	}
}
