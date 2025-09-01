using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[PrimaryKey("TriggerID", "SegmentID")]
[Table("MailTriggerSegment")]
public partial class MailTriggerSegment
{
    [Key]
    public int TriggerID { get; set; }

    [Key]
    public int SegmentID { get; set; }

    public DateTime? CreatedAt { get; set; }
}
