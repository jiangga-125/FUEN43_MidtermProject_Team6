using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	public class MemberToken
	{
		public int MemberTokenID { get; set; }
		public int MemberID { get; set; }

		public string TokenType { get; set; } = null!;   // e.g. "Login.EmailOTP", "Register.EmailCode"
		public string? Token { get; set; }               // NVARCHAR(16)；Demo 版明碼

		// 可選的額外標記/稽核欄位（若資料表沒有這些欄位，請加上或拿掉）
		public string? Purpose { get; set; }
		public string? Ip { get; set; }
		public string? UserAgent { get; set; }

		public DateTime CreatedAt { get; set; }          // 對應表的 CreatedAt
		public DateTime? ExpiresAtUtc { get; set; }      // 對齊表：ExpiresAtUtc
		public DateTime? ConsumedAtUtc { get; set; }     // 對齊表：ConsumedAtUtc

		// ⚠ 如果資料表「沒有」IsRevoked 欄位，請不要保留這個屬性
		// [NotMapped] public bool IsRevoked { get; set; }

		public Member Member { get; set; } = null!;
	}
}
