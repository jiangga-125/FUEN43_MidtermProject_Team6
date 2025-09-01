using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("ReportDefinition")]
[Index("IsActive", "Category", Name = "IX_ReportDefinition_IsActive_Category")]
public partial class ReportDefinition
{
    [Key]
    public int ReportDefinitionID { get; set; }

    [StringLength(100)]
    public string ReportName { get; set; } = null!;

    [StringLength(50)]
    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("ReportDefinition")]
    public virtual ICollection<ReportFilter> ReportFilters { get; set; } = new List<ReportFilter>();
}
