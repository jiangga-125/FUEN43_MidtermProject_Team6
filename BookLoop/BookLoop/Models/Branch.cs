using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class Branch
	{
		[Key]
		public int BranchID { get; set; }
		public string BranchName { get; set; } = null!;
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		public ICollection<BookInventory> Inventories { get; set; } = new List<BookInventory>();
	}
}
