using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class PenaltyRule
{
    public int RuleID { get; set; }

    public string ReasonCode { get; set; } = null!;

    public string ChargeType { get; set; } = null!;

    public int UnitAmount { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<PenaltyTransaction> PenaltyTransactions { get; set; } = new List<PenaltyTransaction>();
}
