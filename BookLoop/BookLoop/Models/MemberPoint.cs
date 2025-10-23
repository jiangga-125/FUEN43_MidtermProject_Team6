using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class MemberPoint
{
	public int MemberPointID { get; set; }   // 主鍵
	public int MemberID { get; set; }        // 外鍵

	public int Points { get; set; }

	public int Balance { get; set; }

	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }

    public virtual Member Member { get; set; } = null!;
}
