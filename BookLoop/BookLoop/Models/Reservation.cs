using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class Reservation
{
    public int ReservationID { get; set; }

    public int ListingID { get; set; }

    public int MemberID { get; set; }

    public DateTime ReservationAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? ReadyAt { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;

    public virtual Member Member { get; set; } = null!;

    public virtual BorrowRecord? BorrowRecord { get; set; } // FK
}
