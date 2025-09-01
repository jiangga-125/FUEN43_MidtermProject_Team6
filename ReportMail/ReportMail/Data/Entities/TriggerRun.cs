using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("TriggerRun")]
[Index("StartTime", Name = "IX_TriggerRun_StartTime")]
public partial class TriggerRun
{
    [Key]
    public long RunID { get; set; }

    public int? TriggerID { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? MatchedCount { get; set; }

    public int? EnqueuedCount { get; set; }

    public int? ErrorCount { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }
}
