using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models;

public partial class OrderDetail
{
	[Display(Name = "訂單明細編號")]
	public int OrderDetailID { get; set; }

	[Display(Name = "訂單編號")]
	public int OrderID { get; set; }

    [Display(Name = "書籍編號")]
	public int BookID { get; set; }

    [Display(Name = "商品名稱")]
	public string ProductName { get; set; } = null!;

    [Display(Name = "數量")]
	public int Quantity { get; set; }

    [Display(Name = "單價")]
	public decimal UnitPrice { get; set; }

	[Display(Name = "商品折扣金額")]
	public decimal? ProductDiscountAmount { get; set; }

	[Display(Name = "建立時間")]
	public DateTime CreatedAt { get; set; }

	//public DateTime UpdatedAt { get; set; }

	public virtual Book Book { get; set; } = null!;

	public virtual Order Order { get; set; } = null!;
}
