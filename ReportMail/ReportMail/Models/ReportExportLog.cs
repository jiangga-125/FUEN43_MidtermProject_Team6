using System;
using System.Collections.Generic;

namespace ReportMail.Models;

public partial class ReportExportLog
{
    public int ExportId { get; set; }

    public int? UserId { get; set; }

    public string? ReportName { get; set; }

    public string? ExportFormat { get; set; }

    public DateTime? ExportAt { get; set; }

    public string? Filters { get; set; }

    public string? FilePath { get; set; }
}
