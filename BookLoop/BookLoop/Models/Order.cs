using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models;

public partial class Order
{
	[Display(Name = "訂單編號")]
	public int OrderID { get; set; }

	[ValidateNever]
	public int? MemberID { get; set; }

	[Display(Name = "顧客編號ID")]
	public int CustomerID { get; set; }

	[Display(Name = "訂單日期")]
	public DateTime OrderDate { get; set; }

	[Display(Name = "總金額")]
	public decimal TotalAmount { get; set; }

	[Display(Name = "訂單狀態")]
	public byte Status { get; set; }

	[Display(Name = "折扣金額")]
	public decimal? DiscountAmount { get; set; }

	[Display(Name = "折扣代碼")]
	public string? DiscountCode { get; set; }

	[Display(Name = "建立時間")]
	public DateTime CreatedAt { get; set; }

	public string? Notes { get; set; }

	[Display(Name = "會員優惠券ID")]
	public long? MemberCouponID { get; set; }

	[Display(Name = "優惠券類型")]
	public byte? CouponTypeSnap { get; set; }

	[Display(Name = "優惠券數值")]
	public decimal? CouponValueSnap { get; set; }

	[Display(Name = "優惠券名稱")]
	public string? CouponNameSnap { get; set; }

	[Display(Name = "優惠券折扣金額")]
	public decimal CouponDiscountAmount { get; set; }

	[Display(Name = "客戶ID號碼")]

	
	[ValidateNever]
	public virtual Customer Customer { get; set; } = null!;

	public virtual MemberCoupon? MemberCoupon { get; set; }

	public virtual Member? Member { get; set; } = null!;

	public virtual ICollection<OrderAddress> OrderAddresses { get; set; } = new List<OrderAddress>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<OrderManagement> OrderManagements { get; set; } = new List<OrderManagement>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

	public virtual ICollection<OrderCouponSnapshot> OrderCouponSnapshots { get; set; } = new List<OrderCouponSnapshot>();
	public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();
}
