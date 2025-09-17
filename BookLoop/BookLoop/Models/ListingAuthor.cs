using System;
using System.Collections.Generic;

namespace BookLoop.Models;


public partial class ListingAuthor
{
    public int ListingAuthorID { get; set; }

    public int ListingID { get; set; }

    public string AuthorName { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;
}
