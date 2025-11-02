using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{    /// <summary>
     /// 寄送日誌：每封信一筆紀錄
     /// 判斷是否為群發：看 MailJobId 是否有值（null = 非群發）
     /// </summary>
    [Table("MailSendLog")]
    public class MailSendLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long LogId { get; set; }
        /// <summary>
        /// 用途類型：Test / System / Bulk
        /// </summary>
        public string Category { get; set; } = "Test";

        /// <summary>
        /// 來源識別（內容/用途）
        /// </summary>
        public int? TemplateId { get; set; }
        public string TemplateKey { get; set; } = string.Empty;

        /// <summary>
        /// 寄出時使用的模板版本（回溯內容版本）
        /// </summary>
        public int? TemplateVersionId { get; set; }

        /// <summary>
        /// 群發批次 Id
        /// </summary>
        public long? MailJobId { get; set; }

        public string Recipient { get; set; } = string.Empty;
        // 與名單明細關聯（讓日誌能 1:1 對到那位收件人）
        public long? JobRecipientId { get; set; } // FK → MailJobRecipient.MailJobRecipientId

        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Pending / Sent / Failed
        /// </summary>
        public string Status { get; set; } = "Sent";

        public string? Error { get; set; }

        /// <summary>
        /// 供外部供應商回寫的 MessageId（若有）
        /// </summary>
        public string? ProviderMsgId { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 寄出當下的 HTML 快照（若你想 Logs/Details 直接顯示寄出樣子）
        /// </summary>
        public string? BodySnapshot { get; set; }
    }
}
