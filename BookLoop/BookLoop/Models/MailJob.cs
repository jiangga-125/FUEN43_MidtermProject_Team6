using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
    /// <summary>
    /// 群發活動/批次：何時、寄給誰、用哪個模板版本。
    /// 判斷是否群發：MailSendLog.MailJobId != null。
    /// </summary>
    [Table("MailJob")]
    public class MailJob
    {
        public long JobId { get; set; }

        // 內容來源
        public int TemplateId { get; set; }
        public int TemplateVersionId { get; set; }

        /// <summary>
        /// 快查用冗餘欄位（對應 Template.TemplateKey）
        /// </summary>
        public string TemplateKey { get; set; } = string.Empty;

        /// <summary>
        /// 活動名稱（例如：NewYear2025）
        /// </summary>
        public string CampaignName { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// 預定寄送時間（MVP 可先忽略，直接「立刻執行」）
        /// </summary>
        public DateTime SendAt { get; set; }

        /// <summary>
        /// 受眾來源（MVP：貼 Email 清單或一段條件字串；後續可改成 SegmentId）
        /// </summary>
        public string SegmentQuery { get; set; } = string.Empty;

        /// <summary>
        /// Scheduled / Running / Done / Cancelled / Failed
        /// </summary>
        public string Status { get; set; } = "Scheduled";

        /// <summary>
        /// 進度與實際起迄
        /// </summary>
        public int TotalRecipients { get; set; } = 0;   // 名單總數
        public int SentCount { get; set; } = 0;         // 已成功寄送數
        public DateTime? StartedAt { get; set; }        // 實際開始時間（第一封送出）
        public DateTime? FinishedAt { get; set; }       // 實際結束時間（最後一封處理完）
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }

        // 導覽屬性
        public ICollection<MailJobRecipient> Recipients { get; set; } = new List<MailJobRecipient>();
    }
}
