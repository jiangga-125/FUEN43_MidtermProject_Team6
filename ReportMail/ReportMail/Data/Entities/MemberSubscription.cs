using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities;

[PrimaryKey("MemberID", "TemplateID")]
[Table("MemberSubscription")]
public partial class MemberSubscription
{
    [Key]
    public int MemberID { get; set; }

    [Key]
    public int TemplateID { get; set; }

    public bool? IsSubscribed { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
