using System;
using System.Collections.Generic;

namespace BookLoop.Models
{
    /// <summary>
    /// 信件模板主檔：代表「來源/用途」(e.g., OrderSuccess, ShippingNotice, Promo_2025Q1_NewYear)
    /// </summary>
    public class Template
    {
        public int TemplateId { get; set; }

        /// <summary>
        /// 唯一識別的 Key（用來區分來源/用途）
        /// </summary>
        public string TemplateKey { get; set; } = string.Empty;

        /// <summary>
        /// 後台顯示名稱；與 Key 分離，方便命名
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 用途/活動說明
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 是否啟用（後台下拉等可過濾）
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    }
}
