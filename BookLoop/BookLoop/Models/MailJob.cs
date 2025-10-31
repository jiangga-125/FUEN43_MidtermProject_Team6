using System;

namespace BookLoop.Models
{
    /// <summary>
    /// 群發活動/批次：何時、寄給誰、用哪個模板版本。
    /// 判斷是否群發：MailSendLog.MailJobId != null。
    /// </summary>
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
        public DateTime SendAtUtc { get; set; }

        /// <summary>
        /// 受眾來源（MVP：貼 Email 清單或一段條件字串；後續可改成 SegmentId）
        /// </summary>
        public string SegmentQuery { get; set; } = string.Empty;

        /// <summary>
        /// Scheduled / Running / Done / Cancelled / Failed
        /// </summary>
        public string Status { get; set; } = "Scheduled";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
    }
}
