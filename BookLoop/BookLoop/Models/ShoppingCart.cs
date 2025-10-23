using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models
{
	public class ShoppingCart
	{
		[Key]
		[Display(Name = "�ʪ����s��")]
		public int CartID { get; set; }

		[Display(Name = "�إߤ��")]
		public DateTime CreatedDate { get; set; } = DateTime.Now;
		public bool IsActive { get; set; } = true;
		
		[Display(Name = "��s���")]
		public DateTime UpdatedDate { get; set; } = DateTime.Now;

		[Display(Name = "�|��ID")]
		public int? MemberID { get; set; }
		
		public virtual Member? Member { get; set; }

		// �����ݩ�
		public virtual ICollection<ShoppingCartItems> Items { get; set; } = new List<ShoppingCartItems>();
	}
}
