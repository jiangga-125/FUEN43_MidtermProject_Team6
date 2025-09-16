using System;
using System.Collections.Generic;

namespace BookLoop.Data.Shop;

public partial class OrderDetail
{
    public int OrderDetailID { get; set; }

    public int OrderID { get; set; }

    public int BookID { get; set; }

    public string ProductName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? ProductDiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Book Book { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
