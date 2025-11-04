using System;

namespace BookLoop.Models
{
	public class MemberTrustedDevice
	{
		public int TrustedDeviceID { get; set; }
		public int MemberID { get; set; }

		public string DeviceHash { get; set; } = null!; // 裝置指紋雜湊
		public string? DeviceName { get; set; }
		public string? Ip { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public bool IsRevoked { get; set; }

		public Member Member { get; set; } = null!;
	}
}