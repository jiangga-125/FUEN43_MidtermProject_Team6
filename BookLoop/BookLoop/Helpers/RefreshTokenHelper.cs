using System.Security.Cryptography;
using System.Text;

namespace BookLoop.Helpers
{
	public class RefreshTokenHelper
	{
		// 產生raw token（Base64）
		public static string GenerateRefreshTokenRaw(int bytes = 64)
		{
			var rnd = new byte[bytes];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(rnd);
			return Convert.ToBase64String(rnd);
		}

		// 用 SHA256 對 raw token 做 hash（並回傳 Base64） -> 存到 DB
		public static string HashRefreshToken(string rawToken)
		{
			using var sha = SHA256.Create();
			var bs = Encoding.UTF8.GetBytes(rawToken);
			var hash = sha.ComputeHash(bs);
			return Convert.ToBase64String(hash);
		}

	}
}
