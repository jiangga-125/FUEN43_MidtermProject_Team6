// Areas/ReportMail/Models/ReportFilters/FiltersIndexVM.cs
using System.Collections.Generic;
using ReportMail.Data.Entities;                 // ← 你的 EF 實體命名空間

namespace ReportMail.Models.ReportFilters
{
    /// <summary>
    /// 篩選欄位列表頁用的 ViewModel：
    /// - Report：目前報表基本資訊（顯示標題用）
    /// - Items ：該報表下所有可編輯的 ReportFilter（已排除底線 meta）
    /// </summary>
    public class FiltersIndexVM
    {
        public ReportDefinition Report { get; set; } = default!;
        public List<ReportFilter> Items { get; set; } = new();
    }
}
