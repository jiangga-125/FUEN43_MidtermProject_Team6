using System.Security.Cryptography;
using System.Text;
using BookLoop.Data;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Generators;

namespace BookLoop.Services;

public class AuthService
{
	private readonly AppDbContext _db;
	public AuthService(AppDbContext db) => _db = db;

	public Task<User?> FindByEmailAsync(string email)
		=> _db.Users.FirstOrDefaultAsync(u => u.Email == email);

	/// <summary>
	/// 驗證密碼：依序嘗試 BCrypt → PBKDF2 → SHA256(hex) → 純文字（僅 demo 備援）
	/// </summary>
	public bool VerifyPassword(User user, string password)
	{
		var stored = user.PasswordHash ?? string.Empty;
		if (string.IsNullOrEmpty(stored)) return false;

		// 1) BCrypt
		if (stored.StartsWith("$2"))
		{
			try { return BCrypt.Net.BCrypt.Verify(password, stored); }
			catch { }
		}

		// 2) PBKDF2 格式：PBKDF2$<iter>$<saltBase64>$<hashBase64>
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

		// 4) 純文字（demo only）
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

	/// <summary>
	/// 回傳使用者最終有效的全部 PermKey：UserPermissions → Permissions
	/// </summary>
	public Task<List<string>> GetEffectivePermKeysAsync(int userId)
		=> _db.UserPermissions
			  .Where(up => up.UserID == userId)
			  .Join(_db.Permissions, up => up.PermissionID, p => p.PermissionID, (up, p) => p.PermKey)
			  .Distinct()
			  .OrderBy(k => k)
			  .ToListAsync();

	/// <summary>
	/// 取得此使用者綁定的供應商 IDs（若有）
	/// </summary>
	public Task<List<int>> GetSupplierIdsAsync(int userId)
		=> _db.SupplierUsers
			  .Where(su => su.UserID == userId)
			  .Select(su => su.SupplierID)
			  .Distinct()
			  .ToListAsync();

	/// <summary>
	/// PBKDF2 密碼產生器（若要換演算法時可用）
	/// </summary>
	public static string HashPasswordPbkdf2(string password, int iterations = 100_000, int saltSize = 16, int keySize = 32)
	{
		var salt = RandomNumberGenerator.GetBytes(saltSize);
		using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
		var hash = pbkdf2.GetBytes(keySize);
		return $"PBKDF2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
	}

	// 讓舊有的呼叫可以編譯通過；目前不記錄登入歷史
	public Task RecordLoginAsync(int userId, bool success, string? ip, string? ua, string? reason = null)
	{
		return Task.CompletedTask;
	}

}
