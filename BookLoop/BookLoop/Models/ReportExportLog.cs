using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

[Table("ReportExportLog")]
[Index("ExportAt", Name = "IX_ReportExportLog_ExportAt")]
public partial class ReportExportLog
{
    [Key]
    public int ExportID { get; set; }

    public int? UserID { get; set; }

    [StringLength(100)]
    public string? ReportName { get; set; }

    [StringLength(20)]
    public string? ExportFormat { get; set; }

    public DateTime? ExportAt { get; set; }

    public string? Filters { get; set; }

    [StringLength(300)]
    public string? FilePath { get; set; }
}
