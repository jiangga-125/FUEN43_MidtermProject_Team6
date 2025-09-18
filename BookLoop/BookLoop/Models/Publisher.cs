using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models;

[Index("PublisherName", Name = "UQ__Publishe__5F0E2249A8A7F082", IsUnique = true)]
[Index("Slug", Name = "UQ__Publishe__BC7B5FB6A09C9CE2", IsUnique = true)]
public partial class Publisher
{
	[Key]
	public int PublisherID { get; set; }

	[StringLength(200)]
	public string PublisherName { get; set; } = null!;

	[StringLength(200)]
	public string? Slug { get; set; }

	public bool IsDeleted { get; set; }

	public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

	[InverseProperty("Publisher")]
	public virtual ICollection<Book> Books { get; set; } = new List<Book>();

	public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
