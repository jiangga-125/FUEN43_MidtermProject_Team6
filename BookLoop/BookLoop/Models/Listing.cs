using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models;

public partial class Listing
{
    public int ListingID { get; set; }

    public int CategoryID { get; set; }

    public int PublisherID { get; set; }


    public string Title { get; set; } = null!;

    public string ISBN { get; set; } = null!;

   
    public string? Condition { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsAvailable { get; set; }=true;

    public virtual ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();


    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ListingAuthor> ListingAuthors { get; set; } = new List<ListingAuthor>();

    public virtual ICollection<ListingImage> ListingImages { get; set; } = new List<ListingImage>();

   
    public virtual Publisher Publisher { get; set; } = null!;

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
