using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	[Table("MailEvent")]
	public class MailEvent
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long MailEventId { get; set; }

		public long MailJobId { get; set; }
		public long JobRecipientId { get; set; }
		public long? LogId { get; set; }              // 對應 MailSendLog.LogId（方便稽核）

		[MaxLength(10)]
		public string EventType { get; set; } = "";   // "Open" / "Click"（未來可擴充 Bounce/Complaint）

		[MaxLength(2000)]
		public string? Url { get; set; }              // Click 才會有

		[MaxLength(1000)]
		public string? UserAgent { get; set; }

		[MaxLength(64)]
		public string? Ip { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.Now;

		// 導覽（可選）
		public MailJob? MailJob { get; set; }
		public MailJobRecipient? JobRecipient { get; set; }
		[ForeignKey(nameof(LogId))]
		public MailSendLog? MailSendLog { get; set; }
	}
}
