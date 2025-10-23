using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BookLoop.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Services;

public class AuthService
{
	private readonly AppDbContext _db;
	private readonly IHttpContextAccessor _http;

	public AuthService(AppDbContext db, IHttpContextAccessor http)
	{
		_db = db;
		_http = http;
	}

	public Task<User?> FindByEmailAsync(string email)
		=> _db.Users.FirstOrDefaultAsync(u => u.Email == email);

	/// <summary>登入：發出精瘦 cookie（permkey + permver），授權時由 Handler 展開 features</summary>
	public async Task SignInAsync(User user, bool isPersistent = true)
	{
		// 取集合鍵（Permissions.PermKey）
		var permKeys = await (
			from up in _db.UserPermissions
			join p in _db.Permissions on up.PermissionID equals p.PermissionID
			where up.UserID == user.UserID
			select p.PermKey
		).Distinct().ToListAsync();

		// 版本戳：變更權限時可更動此值以讓快取失效（可改讀 DB 設定）
		var permVersion = "v1";

		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
			new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
			new Claim(ClaimTypes.Name, user.Email ?? $"user:{user.UserID}"),
			new Claim("permver", permVersion)
		};

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

	/// <summary>（可用於管理頁顯示）取得使用者的 Permission Keys（集合鍵）</summary>
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
}
