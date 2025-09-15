using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class MemberCoupon
{
    public long MemberCouponId { get; set; }

    public int MemberId { get; set; }

    public int CouponId { get; set; }

    public int? AssignedByAdminId { get; set; }

    public DateTime AssignedAt { get; set; }

    public byte Status { get; set; }

    public DateTime? UsedAt { get; set; }

    public long? UsedOrderId { get; set; }

    public Guid? RedeemTxnId { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public bool IsUsed { get; set; }

    public virtual Coupon Coupon { get; set; } = null!;

    public virtual ICollection<OrderCouponSnapshot> OrderCouponSnapshots { get; set; } = new List<OrderCouponSnapshot>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
