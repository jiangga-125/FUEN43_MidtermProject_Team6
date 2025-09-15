using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class OrderCouponSnapshot
{
    public long OrderCouponSnapId { get; set; }

    public int OrderId { get; set; }

    public long? MemberCouponId { get; set; }

    public int CouponId { get; set; }

    public byte CouponTypeSnap { get; set; }

    public decimal CouponValueSnap { get; set; }

    public string? CouponNameSnap { get; set; }

    public decimal CouponDiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual MemberCoupon? MemberCoupon { get; set; }

    public virtual Order Order { get; set; } = null!;
}
