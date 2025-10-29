using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models 
{
    [Table("MailTemplates")] 
    public class MailTemplate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // 告知 EF Core 此欄位由資料庫生成
        public int TemplateID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "範本識別碼 (Key)")]
        public string TemplateKey { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "郵件主旨")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Display(Name = "郵件內容 (HTML)")]
        public string BodyHtml { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "範本說明")]
        public string? Description { get; set; }

        [Required] // 因為資料庫有預設值，標示為 Required
        [Display(Name = "是否啟用")]
        public bool IsActive { get; set; }

        [Required] // 因為資料庫有預設值
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // 由資料庫生成
        public DateTime CreatedAt { get; set; }

        [Required] // 因為資料庫有預設值或 Trigger
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] // 由資料庫生成/更新 (Trigger)
        public DateTime UpdatedAt { get; set; }
    }
}