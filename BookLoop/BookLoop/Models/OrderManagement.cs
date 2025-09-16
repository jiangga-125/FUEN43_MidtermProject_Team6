using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class OrderManagement
{
    public int OrderMgmtID { get; set; }

    public int OrderID { get; set; }

    public byte PaymentStatus { get; set; }

    public byte ShipmentStatus { get; set; }

    public string? Notes { get; set; }

    public string? LastAction { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
