using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Models
{
    [ModelMetadataType(typeof(ReportFilterMeta))]
    public partial class ReportFilter { }

    public class ReportFilterMeta
    {
        [Display(Name = "報表")]
        public int ReportDefinitionId { get; set; }

        [Display(Name = "欄位代號")]
        public string? Key { get; set; }

        [Display(Name = "顯示名稱")]
        public string? Label { get; set; }

        [Display(Name = "排序")]
        public int SortOrder { get; set; }

        [Display(Name = "預設值")]
        public string? DefaultValue { get; set; }

        // 需要隱藏的欄位可加：
        // [ScaffoldColumn(false)]
        // public string? InternalUseOnly { get; set; }
    }
}
