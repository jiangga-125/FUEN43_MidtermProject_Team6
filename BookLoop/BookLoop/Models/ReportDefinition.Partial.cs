using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Models
{
    // 這個 partial 不會被 re-scaffold 覆蓋
    [ModelMetadataType(typeof(ReportDefinitionMeta))]
    public partial class ReportDefinition { }

    // 這裡只放「顯示用」的註解，不會改到資料庫 Schema
    public class ReportDefinitionMeta
    {
        // ↓↓↓ 請把屬性名稱「對應你資料庫產生的實體」來寫 ↓↓↓
        [Display(Name = "報表代碼")]
        public string? Key { get; set; }

        [Display(Name = "報表名稱")]
        [StringLength(100)]
        public string? Name { get; set; }

        [Display(Name = "是否啟用")]
        public bool IsActive { get; set; }

        [Display(Name = "建立時間")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy/MM/dd HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime CreatedAt { get; set; }
    }
}
