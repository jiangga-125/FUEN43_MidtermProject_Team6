using System.Collections.Generic;

namespace BookLoop
{
    public class Supplier
    {
        public int SupplierID { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;

        public ICollection<SupplierUser> SupplierUsers { get; set; } = new List<SupplierUser>();
    }
}
