using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models;

public partial class Return
{
	[Display(Name = "退貨編號")]
	public int ReturnID { get; set; }

	[Display(Name = "訂單編號")]
	public int OrderID { get; set; }

    [Display(Name = "退貨原因")]
	public string? ReturnReason { get; set; }

	[Display(Name = "退貨類型")]
	public byte ReturnType { get; set; }

	[Display(Name = "退貨狀態")]
	public byte Status { get; set; }

	[Display(Name = "申請退貨日期")]
	public DateTime? ReturnedDate { get; set; }

	
	public virtual Order Order { get; set; } = null!;
}
