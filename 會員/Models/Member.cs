using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class Member
{
    public int MemberId { get; set; }

    public string Account { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UserId { get; set; }

    public byte Role { get; set; }

    public int? TotalBooks { get; set; }

    public int? TotalBorrows { get; set; }

    public virtual MemberPoint? MemberPoint { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();

    public virtual ICollection<RuleApplication> RuleApplications { get; set; } = new List<RuleApplication>();
}
