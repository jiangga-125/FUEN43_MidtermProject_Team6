using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailAttachment")]
[Index("MailQueueID", Name = "IX_MailAttachment_MailQueueID")]
public partial class MailAttachment
{
    [Key]
    public int AttachmentID { get; set; }

    public int? MailQueueID { get; set; }

    [StringLength(200)]
    public string? FileName { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }

    [StringLength(500)]
    public string? FilePath { get; set; }

    public byte[]? ContentID { get; set; }

    public DateTime? CreatedAt { get; set; }

    [ForeignKey("MailQueueID")]
    [InverseProperty("MailAttachments")]
    public virtual MailQueue? MailQueue { get; set; }
}
