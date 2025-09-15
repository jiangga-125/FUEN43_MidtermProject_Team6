using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class ReviewModeration
{
    public long ModerationId { get; set; }

    public int ReviewId { get; set; }

    public byte Decision { get; set; }

    public string? Reasons { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime ReviewedAt { get; set; }

    public string? RuleSnapshot { get; set; }
}
