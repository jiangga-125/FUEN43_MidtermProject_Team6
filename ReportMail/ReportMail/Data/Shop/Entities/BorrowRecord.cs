using System;
using System.Collections.Generic;

namespace ReportMail.Data.Shop;

public partial class BorrowRecord
{
    public int RecordID { get; set; }

    public int ListingID { get; set; }

    public int MemberID { get; set; }

    public int? ReservationID { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public DateTime DueDate { get; set; }

    public byte StatusCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;
}
