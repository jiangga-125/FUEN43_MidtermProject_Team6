using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("EventOutbox")]
[Index("Status", "CreatedAt", Name = "IX_EventOutbox_Status_CreatedAt")]
public partial class EventOutbox
{
    [Key]
    public long OutboxID { get; set; }

    [StringLength(50)]
    public string EventType { get; set; } = null!;

    [StringLength(50)]
    public string? EntityType { get; set; }

    public int? EntityID { get; set; }

    public string? PayloadJson { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }
}
