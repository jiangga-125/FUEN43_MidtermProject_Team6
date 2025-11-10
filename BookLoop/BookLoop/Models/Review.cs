using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models;

public partial class Review
{
    public int ReviewID { get; set; }

    public int MemberID { get; set; }

	[Column("DisplayName")]
	[StringLength(50)]
	public string? DisplayName { get; set; }

	public byte TargetType { get; set; } = 0;

    public int TargetID { get; set; }

    public byte Rating { get; set; }

    public string Content { get; set; } = null!;

    public string? ImageUrls { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

	[ForeignKey(nameof(MemberID))]
	public Member? Member { get; set; }   // ✅ 新增這行

}
