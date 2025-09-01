using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailQueue")]
[Index("Status", "CreatedAt", Name = "IX_MailQueue_Status_CreatedAt")]
[Index("TemplateID", Name = "IX_MailQueue_TemplateID")]
public partial class MailQueue
{
    [Key]
    public int MailQueueID { get; set; }

    public int? TemplateID { get; set; }

    [StringLength(250)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    public int Status { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    [InverseProperty("MailQueue")]
    public virtual ICollection<MailAttachment> MailAttachments { get; set; } = new List<MailAttachment>();

    [InverseProperty("MailQueue")]
    public virtual ICollection<MailRecipient> MailRecipients { get; set; } = new List<MailRecipient>();

    [ForeignKey("TemplateID")]
    [InverseProperty("MailQueues")]
    public virtual MailTemplate? Template { get; set; }
}
