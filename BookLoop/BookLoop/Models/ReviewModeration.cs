using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models;

public partial class ReviewModeration
{
    public long ModerationID { get; set; }

    public int ReviewID { get; set; }

    public byte Decision { get; set; }

    public string? Reasons { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime ReviewedAt { get; set; }

    public string? RuleSnapshot { get; set; }

	[ForeignKey(nameof(ReviewID))]
	public Review Review { get; set; }
}
