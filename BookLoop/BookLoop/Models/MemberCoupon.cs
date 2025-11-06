using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	[Table("MemberCoupons")]
	public partial class MemberCoupon
	{
		[Key]
		public long MemberCouponId { get; set; }   // bigint 對應 long

		public int MemberId { get; set; }

		public int CouponId { get; set; }

		public DateTime AssignedAt { get; set; }

		public byte Status { get; set; }

		public DateTime? UsedAt { get; set; }

		public bool IsUsed { get; set; }

		public virtual Coupon Coupon { get; set; } = null!;
		public virtual ICollection<OrderCouponSnapshot> OrderCouponSnapshots { get; set; } = new List<OrderCouponSnapshot>();
		public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
	}
}
