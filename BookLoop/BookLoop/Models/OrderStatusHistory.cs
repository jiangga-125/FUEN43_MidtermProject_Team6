using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class OrderStatusHistory
{
    public int StatusHistoryId { get; set; }

    public int OrderId { get; set; }

    public byte StatusCode { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
