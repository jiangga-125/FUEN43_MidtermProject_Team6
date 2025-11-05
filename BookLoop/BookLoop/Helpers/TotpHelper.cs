using System.Security.Cryptography;
using System.Text;

namespace BookLoop.Helpers
{
	public static class Base32Encoding
	{
		private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

		public static string ToString(byte[] data, bool withPadding = false)
		{
			if (data is null || data.Length == 0) return string.Empty;

			int charCount = (int)Math.Ceiling(data.Length / 5d) * 8;
			var result = new StringBuilder(charCount);

			int buffer = data[0];
			int next = 1;
			int bitsLeft = 8;

			while (bitsLeft > 0 || next < data.Length)
			{
				if (bitsLeft < 5)
				{
					if (next < data.Length)
					{
						buffer <<= 8;
						buffer |= (data[next++] & 0xff);
						bitsLeft += 8;
					}
					else
					{
						int pad = 5 - bitsLeft;
						buffer <<= pad;
						bitsLeft += pad;
					}
				}

				int index = (buffer >> (bitsLeft - 5)) & 0x1f;
				bitsLeft -= 5;
				result.Append(Alphabet[index]);
			}

			if (withPadding)
			{
				int padding = (8 - (result.Length % 8)) % 8;
				if (padding > 0) result.Append('=', padding);
			}

			return result.ToString();
		}

		public static byte[] FromString(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

			string s = input.Trim().TrimEnd('=').ToUpperInvariant();
			var output = new List<byte>();
			int bits = 0, value = 0;

			foreach (char c in s)
			{
				int idx = Alphabet.IndexOf(c);
				if (idx < 0) continue;

				value = (value << 5) | idx;
				bits += 5;

				if (bits >= 8)
				{
					output.Add((byte)((value >> (bits - 8)) & 0xff));
					bits -= 8;
				}
			}
			return output.ToArray();
		}
	}

	public static class TotpHelper
	{
		public static byte[] GenerateSecretBytes(int bytes = 20)
			=> RandomNumberGenerator.GetBytes(bytes);

		public static string GenerateSecretBase32(int bytes = 20, bool withPadding = false)
			=> Base32Encoding.ToString(GenerateSecretBytes(bytes), withPadding);

		public static string GetOtpAuthUri(
			string issuer,
			string account,
			string base32Secret,
			int digits = 6,
			int period = 30,
			string algorithm = "SHA1")
		{
			var iss = Uri.EscapeDataString(issuer ?? "BookLoop");
			var acc = Uri.EscapeDataString(account ?? "member");
			var alg = Uri.EscapeDataString((algorithm ?? "SHA1").ToUpperInvariant());

			return $"otpauth://totp/{iss}:{acc}?secret={base32Secret}&issuer={iss}&digits={digits}&period={period}&algorithm={alg}";
		}

		public static string ComputeTotp(byte[] key, long counter, int digits = 6, string algorithm = "SHA1")
		{
			var counterBytes = BitConverter.GetBytes(counter);
			if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

			byte[] hash = algorithm.ToUpperInvariant() switch
			{
				"SHA256" => new HMACSHA256(key).ComputeHash(counterBytes),
				"SHA512" => new HMACSHA512(key).ComputeHash(counterBytes),
				_ => new HMACSHA1(key).ComputeHash(counterBytes),
			};

			int offset = hash[^1] & 0x0F;
			int binary =
				((hash[offset] & 0x7f) << 24) |
				((hash[offset + 1] & 0xff) << 16) |
				((hash[offset + 2] & 0xff) << 8) |
				(hash[offset + 3] & 0xff);

			int mod = (int)Math.Pow(10, digits);
			int otp = binary % mod;
			return otp.ToString(new string('0', digits));
		}

		public static string ComputeCurrentCode(string base32Secret, int step = 30, int digits = 6, string algorithm = "SHA1")
		{
			var secret = Base32Encoding.FromString(base32Secret);
			long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / step;
			return ComputeTotp(secret, counter, digits, algorithm);
		}

		public static bool VerifyCode(
			string base32Secret,
			string code,
			int step = 30,
			int window = 1,
			int digits = 6,
			string algorithm = "SHA1")
		{
			if (!IsValidNumericCode(code, digits)) return false;

			var secret = Base32Encoding.FromString(base32Secret);
			if (secret.Length == 0) return false;

			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / step;

			for (int w = -window; w <= window; w++)
			{
				var candidate = ComputeTotp(secret, now + w, digits, algorithm);
				if (TimeConstantEquals(candidate, code)) return true;
			}
			return false;
		}

		public static IEnumerable<string> GenerateRecoveryCodes(int count = 10, int length = 10)
		{
			const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
			for (int i = 0; i < count; i++)
			{
				var sb = new StringBuilder(length);
				var bytes = RandomNumberGenerator.GetBytes(length);
				for (int j = 0; j < length; j++)
					sb.Append(chars[bytes[j] % chars.Length]);
				yield return sb.ToString();
			}
		}

		private static bool IsValidNumericCode(string? code, int digits)
			=> !string.IsNullOrWhiteSpace(code) &&
			   code.Length == digits &&
			   code.All(char.IsDigit);

		private static bool TimeConstantEquals(string a, string b)
		{
			if (a == null || b == null || a.Length != b.Length) return false;
			int diff = 0;
			for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
			return diff == 0;
		}
	}
}
