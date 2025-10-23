namespace BookLoop.Models
{
	public class UsedBookInventory
	{
		public int InventoryID { get; set; }
		public int ListingID { get; set; }
		public int BranchID { get; set; }
		public int OnHand { get; set; }
		public int Reserved { get; set; }
		public byte[] RowVersion { get; set; } = Array.Empty<byte>();
		public DateTime UpdatedAt { get; set; }

		public Listing Listing { get; set; } = null!;
		public Branch Branch { get; set; } = null!;
	}
}
