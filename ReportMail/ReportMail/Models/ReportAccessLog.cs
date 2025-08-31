using System;
using System.Collections.Generic;

namespace ReportMail.Models;

public partial class ReportAccessLog
{
    public int AccessId { get; set; }

    public int? UserId { get; set; }

    public int? ReportDefinitionId { get; set; }

    public DateTime AccessedAt { get; set; }

    public string ActionType { get; set; } = null!;

    public string? Ipaddress { get; set; }

    public string? UserAgent { get; set; }

    public virtual ReportDefinition? ReportDefinition { get; set; }
}
