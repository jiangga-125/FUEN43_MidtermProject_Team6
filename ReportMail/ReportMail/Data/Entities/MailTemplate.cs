using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailTemplate")]
[Index("TemplateName", Name = "IX_MailTemplate_TemplateName", IsUnique = true)]
public partial class MailTemplate
{
    [Key]
    public int TemplateID { get; set; }

    [StringLength(100)]
    public string TemplateName { get; set; } = null!;

    [StringLength(200)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    [StringLength(500)]
    public string? Variables { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Template")]
    public virtual ICollection<MailQueue> MailQueues { get; set; } = new List<MailQueue>();
}
