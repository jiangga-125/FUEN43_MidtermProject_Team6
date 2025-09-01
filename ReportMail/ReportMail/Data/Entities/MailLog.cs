using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailLog")]
[Index("MailQueueID", "EventTypeID", "EventAt", Name = "IX_MailLog_MailQueueID_EventTypeID_EventAt")]
public partial class MailLog
{
    [Key]
    public long MailLogID { get; set; }

    public int RecipientID { get; set; }

    public int MailQueueID { get; set; }

    public int EventTypeID { get; set; }

    public DateTime? EventAt { get; set; }

    [StringLength(100)]
    public string? IpAddress { get; set; }

    [StringLength(300)]
    public string? UserAgent { get; set; }
}
