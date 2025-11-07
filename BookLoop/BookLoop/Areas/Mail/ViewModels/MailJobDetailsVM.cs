using BookLoop.Models;

namespace BookLoop.Areas.Mail.ViewModels
{
	public class MailJobDetailsVM
	{
		public MailJob Job { get; set; } = null!;
		public int TotalRecipients { get; set; }
		public int SentCount { get; set; }
		public int UniqueOpens { get; set; }   // 每收件者 OpenCount>0 算 1
		public int UniqueClicks { get; set; }  // 每收件者 ClickCount>0 算 1
	}
}
