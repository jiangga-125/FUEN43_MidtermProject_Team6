using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class PointsLedger
{
    public long PointsLedgerID { get; set; }

    public int MemberID { get; set; }

    public int Delta { get; set; }

    public string? ReasonCode { get; set; }

    public int? OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ExternalOrderNo { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual Order? Order { get; set; }
}
