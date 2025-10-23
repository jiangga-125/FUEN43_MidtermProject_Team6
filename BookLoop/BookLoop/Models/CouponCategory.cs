// Models/CouponCategory.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	[Table("CouponCategories", Schema = "dbo")]
	public class CouponCategory
	{
		public int CouponCategoryID { get; set; }  // PK (IDENTITY)
		public int CouponID { get; set; }          // FK -> Coupons.CouponID
		public int CategoryID { get; set; }        // FK -> Categories.CategoryID

		// 導覽屬性（假設你已有這兩個實體）
		public Coupon? Coupon { get; set; }
		public Category? Category { get; set; }
	}
}
