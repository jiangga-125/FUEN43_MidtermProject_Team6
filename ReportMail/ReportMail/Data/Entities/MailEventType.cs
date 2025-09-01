using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[Table("MailEventType")]
[Index("EventCode", Name = "IX_MailEventType_EventCode", IsUnique = true)]
public partial class MailEventType
{
    [Key]
    public int EventTypeID { get; set; }

    [StringLength(30)]
    public string EventCode { get; set; } = null!;

    [StringLength(100)]
    public string? Description { get; set; }
}
