using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class Review
{
    public int ReviewId { get; set; }

    public int MemberId { get; set; }

    public byte TargetType { get; set; }

    public int TargetId { get; set; }

    public byte Rating { get; set; }

    public string Content { get; set; } = null!;

    public string? ImageUrls { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
