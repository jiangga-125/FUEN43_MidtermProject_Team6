using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("ReportFilter")]
[Index("ReportDefinitionID", "OrderIndex", Name = "IX_ReportFilter_ReportDefinitionID_OrderIndex")]
public partial class ReportFilter
{
    [Key]
    public int ReportFilterID { get; set; }

    public int ReportDefinitionID { get; set; }

    [StringLength(100)]
    public string FieldName { get; set; } = null!;

    [StringLength(100)]
    public string DisplayName { get; set; } = null!;

    [StringLength(20)]
    public string DataType { get; set; } = null!;

    [StringLength(20)]
    public string Operator { get; set; } = null!;

    [StringLength(200)]
    public string ValueJson { get; set; } = "{}";

    public string? Options { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; }=false; 

    public bool IsActive { get; set; }=true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ReportDefinitionID")]
    [InverseProperty("ReportFilters")]
    public virtual ReportDefinition ReportDefinition { get; set; } = null!;
}
