﻿using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class RuleApplication
{
    public int RuleApplicationID { get; set; }

    public string ExternalOrderNo { get; set; } = null!;

    public int MemberID { get; set; }

    public string? CouponCodeSnap { get; set; }

    public byte? CouponTypeSnap { get; set; }

    public decimal? CouponValueSnap { get; set; }

    public decimal CouponDiscountAmount { get; set; }

    public int PointsUsed { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Payable { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? RefundedAt { get; set; }

    public virtual Member Member { get; set; } = null!;
}
