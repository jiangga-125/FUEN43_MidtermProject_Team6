using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class ReviewRuleSettings
{
    public int Id { get; set; }

    public int MinContentLength { get; set; }

    public byte RatingMin { get; set; }

    public byte RatingMax { get; set; }

    public bool BlockSelfReview { get; set; }

    public byte TargetTypeForMember { get; set; }

    public bool ForbidUrls { get; set; }

    public int DuplicateWindowHours { get; set; }

    public byte DuplicatePolicy { get; set; }

    public string? ForbiddenKeywords { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
