using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("ReportAccessLog")]
[Index("AccessedAt", Name = "IX_ReportAccessLog_AccessedAt")]
public partial class ReportAccessLog
{
    [Key]
    public long AccessID { get; set; }

    public int? UserID { get; set; }

    public int? ReportDefinitionID { get; set; }

    public DateTime AccessedAt { get; set; }

    [StringLength(20)]
    public string ActionType { get; set; } = null!;

    [StringLength(64)]
    public string? IPAddress { get; set; }

    public string? UserAgent { get; set; }
}
