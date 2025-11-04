using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
    public class MailJobRecipient
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MailJobRecipientId { get; set; }

        [Required]
        public long MailJobId { get; set; }

        [ForeignKey(nameof(MailJobId))]
        public MailJob MailJob { get; set; } = null!;

        [Required, MaxLength(320)]
        public string RecipientEmail { get; set; } = "";

        [MaxLength(200)]
        public string? RecipientName { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending / Sent / Failed

        public DateTime? SentAt { get; set; }
        [MaxLength(1000)]
        public string? Error { get; set; }

		// === 新追蹤彙總（Brevo 事件回寫用） ===
		public int OpenCount { get; set; } = 0;      // 唯一開啟 => OpenCount > 0
		public int ClickCount { get; set; } = 0;     // 唯一點擊 => ClickCount > 0
		public DateTime? OpenedAt { get; set; }      // 第一次開啟時間
		public DateTime? LastClickAt { get; set; }   // 最近一次點擊時間
	}
}
