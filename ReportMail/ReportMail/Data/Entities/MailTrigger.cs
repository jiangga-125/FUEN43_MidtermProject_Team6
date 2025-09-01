using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailTrigger")]
[Index("IsActive", "TriggerType", "EventName", Name = "IX_MailTrigger_IsActive_TriggerType_EventName")]
public partial class MailTrigger
{
    [Key]
    public int TriggerID { get; set; }

    [StringLength(100)]
    public string TriggerName { get; set; } = null!;

    [StringLength(20)]
    public string TriggerType { get; set; } = null!;

    [StringLength(50)]
    public string? EventName { get; set; }

    public int TemplateID { get; set; }

    [StringLength(20)]
    public string SegmentMode { get; set; } = null!;

    public bool IsActive { get; set; }

    public string? ConditionJson { get; set; }

    public string? VariableMappingsJson { get; set; }

    [StringLength(100)]
    public string? DeduplicateKey { get; set; }

    public int? DeduplicateWindowMinutes { get; set; }

    public int? MaxPerMemberPerDay { get; set; }

    [StringLength(50)]
    public string? ScheduleCron { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Note { get; set; }
}
