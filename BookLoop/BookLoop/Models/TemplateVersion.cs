using System;

namespace BookLoop.Models
{
    /// <summary>
    /// 模板版本：寄信時要鎖定的內容版本
    /// </summary>
    public class TemplateVersion
    {
        public int TemplateVersionId { get; set; }
        public string TemplateName { get; set; } = "";

        public int TemplateId { get; set; }
        public Template? Template { get; set; }

        /// <summary>
        /// 主旨樣板（可含 {{Token}}）
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// 內容 HTML（可含 {{Token}}）
        /// </summary>
        public string BodyHtml { get; set; } = string.Empty;

        /// <summary>
        ///原始設計 JSON
        /// </summary>
        public string? DesignJson { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 發佈/上線時間（與建立時間分離）
        /// </summary>
        public DateTime? PublishedAtUtc { get; set; }
    }
}
