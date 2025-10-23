using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

[Table("ReportDefinition")]
[Index("IsActive", "Category", Name = "IX_ReportDefinition_IsActive_Category")]
public partial class ReportDefinition
{
    [Key]
    public int ReportDefinitionID { get; set; }

    [StringLength(100)]
    public string ReportName { get; set; } = null!;

    [StringLength(50)]
    public string Category { get; set; } = null!;  //line/bar/pie

    public string BaseKind { get; set; } = null!;  // ← 新增：sales|borrow|orders

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystem { get; set; } = false;      //是否預設

    public int SortOrder { get; set; } = 0;          // 排序順序

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("ReportDefinition")]
    public virtual ICollection<ReportFilter> ReportFilters { get; set; } = new List<ReportFilter>();
}
