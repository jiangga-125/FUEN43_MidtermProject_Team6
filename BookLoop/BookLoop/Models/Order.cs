using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int MemberId { get; set; }

    public DateTime OrderDate { get; set; }

    public byte Status { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public decimal? DiscountAmount { get; set; }

    public string? DiscountCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? MemberCouponId { get; set; }

    public byte? CouponTypeSnap { get; set; }

    public decimal? CouponValueSnap { get; set; }

    public string? CouponNameSnap { get; set; }

    public decimal CouponDiscountAmount { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual MemberCoupon? MemberCoupon { get; set; }

    public virtual ICollection<OrderCouponSnapshot> OrderCouponSnapshots { get; set; } = new List<OrderCouponSnapshot>();

    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();
}
