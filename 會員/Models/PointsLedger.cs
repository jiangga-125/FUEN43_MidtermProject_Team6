using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class PointsLedger
{
    public long LedgerId { get; set; }

    public int MemberId { get; set; }

    public int Delta { get; set; }

    public string? ReasonCode { get; set; }

    public int? OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ExternalOrderNo { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual Order? Order { get; set; }
}
