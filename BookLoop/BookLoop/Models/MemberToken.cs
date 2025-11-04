using System;

namespace BookLoop.Models
{
	public class MemberToken
	{
		public int MemberTokenID { get; set; }
		public int MemberID { get; set; }

		public string TokenType { get; set; } = null!; // e.g. "email-otp", "reset"
		public string Token { get; set; } = null!;     // 原樣或加密後字串
		public string? Purpose { get; set; }           // 額外用途標記(選)
		public string? Ip { get; set; }
		public string? UserAgent { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? ExpiresAt { get; set; }
		public bool IsRevoked { get; set; }

		public Member Member { get; set; } = null!;
	}
}
