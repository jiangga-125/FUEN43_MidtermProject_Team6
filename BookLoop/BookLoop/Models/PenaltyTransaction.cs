using System;
using System.Collections.Generic;

namespace BookLoop.Models;


public partial class PenaltyTransaction
{
    public int PenaltyID { get; set; }

    public int RecordID { get; set; }

    public int MemberID { get; set; }

    public int RuleID { get; set; }

    public DateTime CreatedAt { get; set; }

    public int Quantity { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual BorrowRecord Record { get; set; } = null!;

    public virtual PenaltyRule Rule { get; set; } = null!;
}
