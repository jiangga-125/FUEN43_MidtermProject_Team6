using System;
using System.Collections.Generic;

namespace BookLoop.BorrowSystem.Models;

public partial class ListingImage
{
    public int ImageID { get; set; }

    public int ListingID { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;
}
