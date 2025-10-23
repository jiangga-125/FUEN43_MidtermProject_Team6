using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class OrderStatusHistory
{
    public int OrderStatusHistoryID { get; set; }

    public int OrderID { get; set; }

    public byte StatusCode { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
