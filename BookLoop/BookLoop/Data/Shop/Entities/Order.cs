using System;
using System.Collections.Generic;

namespace BookLoop.Data.Shop;

public partial class Order
{
    public int OrderID { get; set; }

    public int CustomerID { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public byte Status { get; set; }

    public decimal? DiscountAmount { get; set; }

    public string? DiscountCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? MemberCouponID { get; set; }

    public byte? CouponTypeSnap { get; set; }

    public decimal? CouponValueSnap { get; set; }

    public string? CouponNameSnap { get; set; }

    public decimal CouponDiscountAmount { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
