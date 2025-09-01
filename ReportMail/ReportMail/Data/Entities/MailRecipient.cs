using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailRecipient")]
[Index("MailQueueID", "Status", Name = "IX_MailRecipient_MailQueueID_Status")]
public partial class MailRecipient
{
    [Key]
    public int RecipientID { get; set; }

    public int MailQueueID { get; set; }

    public int? MemberID { get; set; }

    [StringLength(254)]
    public string SendTo { get; set; } = null!;

    public int Status { get; set; }

    public DateTime? SentAt { get; set; }

    [StringLength(500)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("MailQueueID")]
    [InverseProperty("MailRecipients")]
    public virtual MailQueue MailQueue { get; set; } = null!;
}
