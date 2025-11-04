using System;

namespace BookLoop.Models
{
	public class MemberLogin
	{
		public int MemberLoginID { get; set; }
		public int MemberID { get; set; }
		public string Provider { get; set; } = null!;
		public string ProviderKey { get; set; } = null!;
		public string? DisplayName { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public Member Member { get; set; } = null!;
	}
}
