using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	public class ReviewReport
	{
		[Key]
		public int ReportID { get; set; }

		public int ReviewID { get; set; }
		public int ReporterID { get; set; }

		[Required, StringLength(200)]
		public string Reason { get; set; }

		[StringLength(500)]
		public string? Description { get; set; }

		public byte Status { get; set; } = 0; // 0=待審, 1=成立, 2=駁回

		public DateTime CreatedAt { get; set; } = DateTime.Now;
		public DateTime? ReviewedAt { get; set; }
		public int? ReviewedBy { get; set; }

		[ForeignKey(nameof(ReviewID))]
		public Review Review { get; set; }

		[ForeignKey(nameof(ReporterID))]
		public Member Reporter { get; set; }
	}
}
