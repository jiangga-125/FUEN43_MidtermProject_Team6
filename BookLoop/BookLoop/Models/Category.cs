using System;
using System.Collections.Generic;


namespace BookLoop.Models;

public partial class Category
{
	public int CategoryID { get; set; }

	public string? Code { get; set; }

	public string CategoryName { get; set; } = null!;

	public int? ParentID { get; set; }

	public string? Slug { get; set; } 

	public int SortOrder { get; set; }

	public bool IsDeleted { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

	public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();

	public virtual Category? Parent { get; set; }
}
