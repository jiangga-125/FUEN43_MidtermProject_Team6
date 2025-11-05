using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	[Table("IntegrationCursor")]
	public class IntegrationCursor
	{
		[Key]
		[MaxLength(100)]
		public string CursorKey { get; set; } = "";   // 例： "Brevo:Events"

		public DateTime CursorTime { get; set; }       // 上次成功抓完的時間
	}
}
