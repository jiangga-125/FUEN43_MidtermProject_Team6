using System;
using System.Collections.Generic;

namespace BookLoop.BorrowSystem.Models;

public partial class Category
{
    public int CategoryID { get; set; }

    public string? Code { get; set; }

    public string CategoryName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
