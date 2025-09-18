namespace BookLoop
{
    public class SupplierUser
    {
        public int SupplierID { get; set; }
        public int UserID { get; set; }

        public Supplier? Supplier { get; set; }
        public User? User { get; set; }
    }
}
