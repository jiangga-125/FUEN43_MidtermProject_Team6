using BookLoop.Models;
using System;
using System.Collections.Generic;

namespace BookLoop.Data.Shop;

public partial class Book
{
    public int BookID { get; set; }

    public string ISBN { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public int? AuthorID { get; set; }

    public int PublisherID { get; set; }

    public int CategoryID { get; set; }

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public decimal ListPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public DateOnly? PublishDate { get; set; }

    public string? LanguageCode { get; set; }

    public byte Status { get; set; }

    public bool IsListed { get; set; }

    public DateTime? ListedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
