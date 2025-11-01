namespace BookLoop.Models.ViewModels
{
	public class ReviewModerationUnifiedVM
	{
		public int ReviewID { get; set; }
		public string Content { get; set; }
		public string Reason { get; set; }
		public string Source { get; set; } // 系統審核 / 會員檢舉
		public string? ReporterName { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
