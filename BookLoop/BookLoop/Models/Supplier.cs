using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop
{
    public class Supplier
    {
        public int SupplierID { get; set; }

		[NotMapped]
		public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;

        public ICollection<SupplierUser> SupplierUsers { get; set; } = new List<SupplierUser>();
    }
}
