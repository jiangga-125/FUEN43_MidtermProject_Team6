// File: Services/PasswordHasher.cs
using System.Security.Cryptography;
using System.Text;

namespace BookLoop.Services;

public static class PasswordHasher
{
	// 產生：{iterations}.{saltBase64}.{hashBase64}
	public static string Hash(string password, int iterations = 100_000)
	{
		using var rng = RandomNumberGenerator.Create();
		var salt = new byte[16];
		rng.GetBytes(salt);

		using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
		var hash = pbkdf2.GetBytes(32);
		return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
	}

	public static bool Verify(string password, string stored)
	{
		var parts = stored.Split('.', 3);
		if (parts.Length != 3) return false;
		if (!int.TryParse(parts[0], out int iterations)) return false;

		var salt = Convert.FromBase64String(parts[1]);
		var expected = Convert.FromBase64String(parts[2]);

		using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
		var actual = pbkdf2.GetBytes(32);
		return CryptographicOperations.FixedTimeEquals(expected, actual);
	}
}
