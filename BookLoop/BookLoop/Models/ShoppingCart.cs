using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class ShoppingCart
	{
		[Key]
		[Display(Name = "購物車編號")]
		public int CartID { get; set; }

		[Display(Name = "建立日期")]
		public DateTime CreatedDate { get; set; } = DateTime.Now;
		public bool IsActive { get; set; } = true;
		
		[Display(Name = "更新日期")]
		public DateTime UpdatedDate { get; set; } = DateTime.Now;

		[Display(Name = "會員ID")]
		public int? MemberID { get; set; }
		
		public virtual Member? Member { get; set; }

		// 導覽屬性
		public virtual ICollection<ShoppingCartItems> Items { get; set; } = new List<ShoppingCartItems>();
	}
}
