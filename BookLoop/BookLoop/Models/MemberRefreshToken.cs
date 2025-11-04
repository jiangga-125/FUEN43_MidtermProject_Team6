using System;

namespace BookLoop.Models
{
	public class MemberRefreshToken
	{
		public int Id { get; set; }
		public int MemberId { get; set; }

		public string TokenHash { get; set; } = null!; // 存 SHA-256 Base64
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime ExpiresAt { get; set; }
		public bool IsRevoked { get; set; }

		public string? Ip { get; set; }
		public string? UserAgent { get; set; }
	}
}