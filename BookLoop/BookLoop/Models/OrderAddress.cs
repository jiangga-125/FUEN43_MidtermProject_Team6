using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class OrderAddress
{
    public int OrderAddressId { get; set; }

    public int OrderId { get; set; }

    public byte AddressType { get; set; }

    public string Address { get; set; } = null!;

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
}
