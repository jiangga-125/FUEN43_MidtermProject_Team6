using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class Coupon
{
    public int CouponId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public byte DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public bool IsActive { get; set; }

    public int? MaxUsesTotal { get; set; }

    public int? MaxUsesPerMember { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public bool RequireLogin { get; set; }

    public bool AutoAssignOnRegister { get; set; }

    public virtual ICollection<MemberCoupon> MemberCoupons { get; set; } = new List<MemberCoupon>();

    public virtual ICollection<OrderCouponSnapshot> OrderCouponSnapshots { get; set; } = new List<OrderCouponSnapshot>();
}
