using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using BookLoop.Data;
using BookLoop.Helpers;   // RefreshTokenHelper
using BookLoop.Models;    // RefreshToken & User
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BookLoop.Services
{
	/// <summary>
	/// 後台 Users 專用的登入/授權/JWT/RefreshToken 服務。
	/// （前台 Members 會用獨立的 AuthMemberService 與 MemberRefreshTokens）
	/// </summary>
	public class AuthService
	{
		private readonly AppDbContext _db;
		private readonly IHttpContextAccessor _http;
		private readonly IConfiguration _cfg;

		public AuthService(AppDbContext db, IHttpContextAccessor http, IConfiguration cfg)
		{
			_db = db;
			_http = http;
			_cfg = cfg;
		}

		public Task<User?> FindByEmailAsync(string email)
			=> _db.Users.FirstOrDefaultAsync(u => u.Email == email);

		/// <summary>後台 Cookie 登入（精瘦 claims：permkey + permver）</summary>
		public async Task SignInAsync(User user, bool isPersistent = true)
		{
			// 取集合鍵（Permissions.PermKey）
			var permKeys = await (
				from up in _db.UserPermissions
				join p in _db.Permissions on up.PermissionID equals p.PermissionID
				where up.UserID == user.UserID
				select p.PermKey
			).Distinct().ToListAsync();

			var permVersion = "v1"; // 權限快取版號

			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
				new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
				new Claim(ClaimTypes.Name, user.Email ?? $"user:{user.UserID}"),
				new Claim("permver", permVersion),

				new Claim(ClaimTypes.Role, "Member")
			};

			// 供報表資料範圍使用（你原本的需求）
			var supplierIds = await GetSupplierIdsAsync(user.UserID);
			if (supplierIds.Any())
			{
				claims.Add(new Claim("supplier", supplierIds.First().ToString()));
			}

			// 精瘦：只放集合鍵
			claims.AddRange(permKeys.Select(k => new Claim("permkey", k)));

			var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
			var principal = new ClaimsPrincipal(identity);

			await _http.HttpContext!.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				principal,
				new AuthenticationProperties
				{
					IsPersistent = isPersistent,
					ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
					AllowRefresh = true
				});
		}

		public async Task SignOutAsync()
			=> await _http.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

		public Task<List<string>> GetEffectivePermKeysAsync(int userId)
			=> _db.UserPermissions
				.Where(up => up.UserID == userId)
				.Join(_db.Permissions, up => up.PermissionID, p => p.PermissionID, (up, p) => p.PermKey)
				.Distinct().OrderBy(k => k).ToListAsync();

		public Task<List<int>> GetSupplierIdsAsync(int userId)
			=> _db.SupplierUsers.Where(su => su.UserID == userId)
				.Select(su => su.SupplierID).Distinct().ToListAsync();

		// ====== 密碼驗證（保留你的版本） ======
		public bool VerifyPassword(User user, string password)
		{
			var stored = user.PasswordHash ?? string.Empty;
			if (string.IsNullOrEmpty(stored)) return false;

			// 1) BCrypt
			if (stored.StartsWith("$2"))
			{
				try { return BCrypt.Net.BCrypt.Verify(password, stored); } catch { }
			}
			// 2) PBKDF2: PBKDF2$<iter>$<saltBase64>$<hashBase64>
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
			// 3) SHA256 hex
			if (stored.Length == 64 && IsHex(stored))
			{
				var sha = SHA256.HashData(Encoding.UTF8.GetBytes(password));
				var hex = ToHex(sha);
				return string.Equals(stored, hex, StringComparison.OrdinalIgnoreCase);
			}
			// 4) 純文字 demo
			return stored == password;

			static bool IsHex(string s) => s.All(c =>
				(c >= '0' && c <= '9') ||
				(c >= 'a' && c <= 'f') ||
				(c >= 'A' && c <= 'F'));

			static string ToHex(byte[] bytes)
			{
				var sb = new StringBuilder(bytes.Length * 2);
				foreach (var b in bytes) sb.Append(b.ToString("x2"));
				return sb.ToString();
			}
		}

		public static string HashPasswordPbkdf2(string password, int iterations = 100_000, int saltSize = 16, int keySize = 32)
		{
			var salt = RandomNumberGenerator.GetBytes(saltSize);
			using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
			var hash = pbkdf2.GetBytes(keySize);
			return $"PBKDF2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
		}

		public Task RecordLoginAsync(int userId, bool success, string? ip, string? ua, string? reason = null)
			=> Task.CompletedTask;

		// ====== Users（後台）JWT / Refresh ======

		public async Task<User?> FindByIdAsync(int id)
			=> await _db.Users.FirstOrDefaultAsync(u => u.UserID == id);

		/// <summary>產生後台 Users 的 Access Token（JWT）</summary>
		public string CreateAccessToken(User user)
		{
			var jwtSection = _cfg.GetSection("Jwt");
			var keyStr = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key 未設定");
			var issuer = jwtSection["Issuer"];
			var audience = jwtSection["Audience"];
			var accessMinutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "15");

			SymmetricSecurityKey signingKey;
			try { signingKey = new SymmetricSecurityKey(Convert.FromBase64String(keyStr)); }
			catch { signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr)); }

			string uid = user.UserID.ToString();
			string email = user.Email ?? string.Empty;
			string name = user.Email ?? $"user:{user.UserID}";

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

			return new JwtSecurityTokenHandler().WriteToken(jwt);
		}

		/// <summary>建立（後台 Users 用）Refresh Token，DB 僅存雜湊</summary>
		public async Task<(string raw, RefreshToken entity)> CreateAndStoreRefreshTokenAsync(User user, int days = 30)
		{
			var raw = RefreshTokenHelper.GenerateRefreshTokenRaw();
			var hash = RefreshTokenHelper.HashRefreshToken(raw);

			var rt = new RefreshToken
			{
				UserID = user.UserID,
				TokenHash = hash,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddDays(days),
				IsRevoked = false
			};

			_db.RefreshTokens.Add(rt);
			await _db.SaveChangesAsync();

			return (raw, rt);
		}

		/// <summary>依 raw token 撤銷（後台 Users 用）</summary>
		public async Task RevokeRefreshTokenByRawAsync(string raw)
		{
			var hash = RefreshTokenHelper.HashRefreshToken(raw);
			var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash && !r.IsRevoked);
			if (rt != null)
			{
				rt.IsRevoked = true;
				await _db.SaveChangesAsync();
			}
		}
	}
}
