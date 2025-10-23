using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace BookLoop.Models;

[Index("CategoryName", Name = "UQ__Categori__8517B2E06C188FB6", IsUnique = true)]
[Index("Slug", Name = "UQ__Categori__BC7B5FB6B51AFD6A", IsUnique = true)]
public partial class Category
{
	[Key]
	public int CategoryID { get; set; }

	[StringLength(100)]
	public string CategoryName { get; set; } = null!;

	[StringLength(200)]
	public string? Slug { get; set; }

	public bool IsDeleted { get; set; }

	public string? Code { get; set; }

	public int? ParentID { get; set; }

	//public int SortOrder { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	[InverseProperty("Category")]
	public virtual ICollection<Book> Books { get; set; } = new List<Book>();

	public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

	public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();

	public virtual Category? Parent { get; set; }

	public virtual ICollection<CouponCategory> CouponCategories { get; set; } = new List<CouponCategory>();
}
