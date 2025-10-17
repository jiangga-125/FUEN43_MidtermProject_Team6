using System;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class BookInventory
	{
		[Key]
		public int InventoryID { get; set; }
		public int BookID { get; set; }
		public int BranchID { get; set; }
		public int OnHand { get; set; }
		public int Reserved { get; set; }
		public byte[] RowVersion { get; set; } = Array.Empty<byte>();
		public DateTime UpdatedAt { get; set; }

		public Book Book { get; set; } = null!;
		public Branch Branch { get; set; } = null!;
	}
}
