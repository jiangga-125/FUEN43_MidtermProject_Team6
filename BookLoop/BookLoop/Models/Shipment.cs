using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class Shipment
{
    public int ShipmentId { get; set; }

    public int OrderId { get; set; }

    public string? Provider { get; set; }

    public int? AddressId { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? ShippedDate { get; set; }

    public DateTime? DeliveredDate { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual OrderAddress? Address { get; set; }

    public virtual Order Order { get; set; } = null!;
}
