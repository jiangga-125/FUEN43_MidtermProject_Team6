using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using BookLoop.Models;

namespace BookLoop
{
	public class Member
	{
		public int MemberID { get; set; }
		public int? UserID { get; set; } // 舊欄位：保留但前台認證不再使用

		[NotMapped]
		public string? Account { get; set; } = null!;

		public string Username { get; set; } = null!;
		public string? Email { get; set; }
		public string? Phone { get; set; }

		public byte Role { get; set; }          // 0=一般,1=管理會員(保留)
		public byte Status { get; set; }        // 0=未啟用,1=啟用,2=停權,3=關閉

		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public int TotalBooks { get; set; }
		public int TotalBorrows { get; set; }

		// 併發控制
		public byte[] RowVersion { get; set; } = Array.Empty<byte>();

		// ===== 前台認證/2FA 必要欄位 =====
		public string? EmailNormalized { get; set; }   // UPPER(email)
		public bool EmailConfirmed { get; set; }       // 驗信
		public string? PasswordHash { get; set; }      // 本地密碼（可採用 Identity 格式或自定 PBKDF2/BCrypt）
		public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

		public bool PhoneConfirmed { get; set; }
		public bool TwoFactorEnabled { get; set; }

		// TOTP 秘鑰（建議應用層加密後再存）；你也可改成 string(Base32) 儲存
		public byte[]? AuthenticatorKey { get; set; }

		public int AccessFailedCount { get; set; }
		public bool LockoutEnabled { get; set; } = true;
		public DateTime? LockoutEndUtc { get; set; }
		public DateTime? LastLoginAt { get; set; }

		// ===== 你的既有關聯：保留 =====
		public ICollection<MemberPoint> MemberPoints { get; set; } = new List<MemberPoint>();
		public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
		public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();
		public virtual ICollection<RuleApplication> RuleApplications { get; set; } = new List<RuleApplication>();
		public virtual ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
		public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
		public virtual ICollection<PenaltyTransaction> PenaltyTransactions { get; set; } = new List<PenaltyTransaction>();

		// 外部登入關聯
		public ICollection<MemberLogin> Logins { get; set; } = new List<MemberLogin>();
	}

	// ===== 前台外部登入綁定 =====
	public class MemberLogin
	{
		public int MemberLoginID { get; set; }
		public int MemberID { get; set; }
		public string Provider { get; set; } = null!;     // Google/Facebook/LINE
		public string ProviderKey { get; set; } = null!;  // sub/userId 等
		public string? DisplayName { get; set; }
		public DateTime CreatedAt { get; set; }

		public Member Member { get; set; } = null!;
	}

	// ===== 前台一次性 Token（驗信、重設密碼、2FA 中繼）=====
	public class MemberToken
	{
		public int MemberTokenID { get; set; }
		public int MemberID { get; set; }
		public string TokenType { get; set; }     // 1=EmailConfirm, 2=ResetPassword, 3=TwoFactorSession
		public string? Token { get; set; } = null!;
		public DateTime ExpiresAtUtc { get; set; }
		public DateTime? ConsumedAtUtc { get; set; }
		public DateTime CreatedAt { get; set; }

		public Member Member { get; set; } = null!;
	}

	// ===== 前台「記住此裝置」 =====
	public class MemberTrustedDevice
	{
		public int TrustedDeviceID { get; set; }
		public int MemberID { get; set; }
		public string DeviceHash { get; set; } = null!;
		public string? DeviceName { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? LastUsedAt { get; set; }
		public DateTime ExpiresAtUtc { get; set; }

		public Member Member { get; set; } = null!;
	}

	// ===== 前台 2FA 復原碼（僅存雜湊） =====
	public class MemberRecoveryCode
	{
		public int MemberRecoveryCodeID { get; set; }
		public int MemberID { get; set; }
		public string CodeHash { get; set; } = null!;
		public DateTime? UsedAtUtc { get; set; }
		public DateTime CreatedAt { get; set; }

		public Member Member { get; set; } = null!;
	}

	// ===== 前台 JWT Refresh Token（僅存雜湊） =====
	public class MemberRefreshToken
	{
		public int Id { get; set; }
		public int MemberId { get; set; }
		public string TokenHash { get; set; } = null!;
		public DateTime CreatedAt { get; set; }
		public DateTime ExpiresAt { get; set; }
		public bool IsRevoked { get; set; }
	}
}
