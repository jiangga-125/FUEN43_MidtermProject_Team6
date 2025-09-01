using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailHistory")]
[Index("SentAt", Name = "IX_MailHistory_SentAt")]
public partial class MailHistory
{
    [Key]
    public int HistoryID { get; set; }

    public int RecipientID { get; set; }

    public int MailQueueID { get; set; }

    public int? TemplateID { get; set; }

    [StringLength(254)]
    public string? SendTo { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    public DateTime? SentAt { get; set; }

    public int? DeliveryStatus { get; set; }
}
