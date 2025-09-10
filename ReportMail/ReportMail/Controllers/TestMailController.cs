using Microsoft.AspNetCore.Mvc;
using ReportMail.Services;

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class TestMailController : Controller
	{
		private readonly MailService _mail;
		public TestMailController(MailService mail) => _mail = mail;

		public IActionResult SendTest()
		{
			try
			{
				_mail.SendReport(
					"hywgant@gmail.com",
					 "Brevo 測試信",
					"您好！這是來自 ReportMail 系統的測試信。",
					"C:\\Users\\ispan\\Downloads\\train_data_titanic.csv"  // 你測試匯出的 Excel 檔路徑
				);
				return Content("✔ 測試信發送成功！");
			}
			catch (Exception ex)
			{
				return Content("❌ 發送失敗：" + ex.Message);
			}
		}
	}
}