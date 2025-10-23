using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	public partial class BookImage
	{
		[Key]
		public int ImageID { get; set; }

		public int BookID { get; set; }

		[StringLength(500)]
		public string FilePath { get; set; } = null!;

		[StringLength(200)]
		public string? Caption { get; set; }   // ✅ 補上

		public bool IsPrimary { get; set; }

		public DateTime CreatedAt { get; set; }

		public DateTime UpdatedAt { get; set; }   // ✅ 補上

		[ForeignKey("BookID")]
		[InverseProperty("BookImages")]
		public virtual Book Book { get; set; } = null!;
	}
}
