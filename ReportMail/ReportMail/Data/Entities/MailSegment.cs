using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailSegment")]
[Index("IsActive", Name = "IX_MailSegment_IsActive")]
public partial class MailSegment
{
    [Key]
    public int SegmentID { get; set; }

    [StringLength(100)]
    public string SegmentName { get; set; } = null!;

    public string? Description { get; set; }

    public string? FilterJson { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastEvaluatedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
