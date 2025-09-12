using System;
using System.Collections.Generic;

namespace BookLoop.Data.Shop;

public partial class Listing
{
    public int ListingID { get; set; }

    public int CategoryID { get; set; }

    public int PublisherID { get; set; }

    public string Title { get; set; } = null!;

    public string ISBN { get; set; } = null!;

    public string? Condition { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsAvailable { get; set; }

    public virtual ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();

    public virtual Category Category { get; set; } = null!;
}
