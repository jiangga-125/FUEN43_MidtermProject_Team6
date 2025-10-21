using System;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class ShoppingCartItems
	{
		[Key]
		public int ItemID { get; set; }

		public int CartID { get; set; }      // ���� ShoppingCart
		public int BookID { get; set; }      // ���� Book
		public int Quantity { get; set; } = 1;
		public decimal UnitPrice { get; set; }

		// �����ݩ�
		public virtual ShoppingCart Cart { get; set; }
		// ���] Book �ҫ��s�b
		public virtual Book Book { get; set; }
	}
}
