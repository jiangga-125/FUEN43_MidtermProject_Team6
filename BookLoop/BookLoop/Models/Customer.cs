using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class Customer
{
    public int CustomerId { get; set; }

    public int? MemberId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
