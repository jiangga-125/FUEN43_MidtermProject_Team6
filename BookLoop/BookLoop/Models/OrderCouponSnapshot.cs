using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models;

public partial class OrderCouponSnapshot
{
	[Key]
	public long OrderCouponSnapID { get; set; }

    public int OrderID { get; set; }

    public long? MemberCouponID { get; set; }

    public int CouponID { get; set; }

    public byte CouponTypeSnap { get; set; }

    public decimal CouponValueSnap { get; set; }

    public string? CouponNameSnap { get; set; }

    public decimal CouponDiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual MemberCoupon? MemberCoupon { get; set; }

    public virtual Order Order { get; set; } = null!;
}
