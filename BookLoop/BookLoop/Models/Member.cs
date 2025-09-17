using System;
using System.Collections.Generic;

namespace BookLoop.BorrowSystem.Models;

public partial class Member
{
    public int MemberID { get; set; }

    public int? UserID { get; set; }

    public string Account { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public byte Role { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();

    public virtual ICollection<PenaltyTransaction> PenaltyTransactions { get; set; } = new List<PenaltyTransaction>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
