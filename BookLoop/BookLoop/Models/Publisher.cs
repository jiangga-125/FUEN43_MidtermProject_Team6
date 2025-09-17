using System;
using System.Collections.Generic;

namespace BookLoop.Models;


public partial class Publisher
{
    public int PublisherID { get; set; }

    public string PublisherName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
