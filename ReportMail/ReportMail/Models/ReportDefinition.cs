using System;
using System.Collections.Generic;

namespace ReportMail.Models;

public partial class ReportDefinition
{
    public int ReportDefinitionId { get; set; }

    public string ReportName { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ReportAccessLog> ReportAccessLogs { get; set; } = new List<ReportAccessLog>();

    public virtual ICollection<ReportFilter> ReportFilters { get; set; } = new List<ReportFilter>();
}
