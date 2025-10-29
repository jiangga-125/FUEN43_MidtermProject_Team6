using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
	[Table("Advertisements")]
	public class Advertisement
	{
		[Key]
		public int AdvertisementID { get; set; }   // 主鍵

		[Required, StringLength(100)]
		public string Title { get; set; } = null!; // 廣告標題

		[Required, StringLength(255)]
		public string ImageUrl { get; set; } = null!; // 圖片路徑

		[StringLength(255)]
		public string? LinkUrl { get; set; } // 點擊連結

		public DateTime? StartAt { get; set; } // 開始時間
		public DateTime? EndAt { get; set; }   // 結束時間

		public bool IsActive { get; set; } = true; // 是否啟用

		public int DisplayOrder { get; set; } = 0; // 顯示順序

		[Required, StringLength(50)]
		public string Position { get; set; } = "HomeBanner"; // 顯示位置

		public DateTime CreatedAt { get; set; } = DateTime.Now;
		public DateTime UpdatedAt { get; set; } = DateTime.Now;
	}
}
