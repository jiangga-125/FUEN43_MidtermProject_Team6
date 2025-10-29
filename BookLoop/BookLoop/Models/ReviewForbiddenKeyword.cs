using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	public class ReviewForbiddenKeyword
	{
		[Key] public int Id { get; set; }

		[Required, StringLength(50)]
		[Display(Name = "禁用詞")]

		[Column("word")]public string Keyword { get; set; } = string.Empty;

		[Display(Name = "嚴重程度")]
		public byte Severity { get; set; } = 1;

		[Display(Name = "說明")]
		public string? Description { get; set; }

		[Display(Name = "是否啟用")]
		public bool IsActive { get; set; } = true;

		[Display(Name = "建立時間")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		[Display(Name = "更新時間")]
		public DateTime UpdatedAt { get; set; } = DateTime.Now;
	}
}
