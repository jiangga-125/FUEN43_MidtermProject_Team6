using System;
using System.Collections.Generic;

namespace BookLoop.Models;

public partial class Review
{
    public int ReviewID { get; set; }

    public int MemberID { get; set; }

    public byte TargetType { get; set; } = 0;

    public int TargetID { get; set; }

    public byte Rating { get; set; }

    public string Content { get; set; } = null!;

    public string? ImageUrls { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
