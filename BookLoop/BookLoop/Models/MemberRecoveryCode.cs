using System;

namespace BookLoop.Models
{
	public class MemberRecoveryCode
	{
		public int MemberRecoveryCodeID { get; set; }
		public int MemberID { get; set; }

		public string Code { get; set; } = null!;   // 8~10 碼
		public string? Purpose { get; set; }        // e.g. "2fa-recovery"
		public bool IsUsed { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UsedAt { get; set; }

		public Member Member { get; set; } = null!;
	}
}