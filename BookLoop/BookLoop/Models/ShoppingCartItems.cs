using System;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class ShoppingCartItems
	{
		[Key]
		public int ItemID { get; set; }

		public int CartID { get; set; }      // 對應 ShoppingCart
		public int BookID { get; set; }      // 對應 Book
		public int Quantity { get; set; } = 1;
		public decimal UnitPrice { get; set; }

		// 導覽屬性
		public virtual ShoppingCart Cart { get; set; }
		// 假設 Book 模型存在
		public virtual Book Book { get; set; }
	}
}
