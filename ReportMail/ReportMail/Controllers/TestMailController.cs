//用 MailKit 測試 SMTP，功能完整，可以確認 Brevo SMTP 配置是否正確
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class TestMailController : Controller
	{
		private readonly IConfiguration _config;
		public TestMailController(IConfiguration config) => _config = config;

		/// <summary>
		/// 測試 Brevo SMTP 寄信
		/// 使用方式：/ReportMail/TestMail/Send?to=收件人Email
		/// </summary>
		[HttpGet]
		public IActionResult Send(string to)
		{
			if (string.IsNullOrWhiteSpace(to))
				return BadRequest("請在 URL 加上 ?to=收件人Email");

			try
			{
				var smtp = _config.GetSection("Smtp");

				var fromEmail = smtp["FromEmail"];
				var fromName = smtp["FromName"] ?? "Report Mailer";
				var userName = smtp["UserName"];   // 96a426002@smtp-brevo.com
				var password = smtp["Password"];   // Brevo SMTP Key
				var host = smtp["Host"] ?? "smtp-relay.brevo.com";
				var port = int.Parse(smtp["Port"] ?? "587");

				var msg = new MimeMessage();
				msg.From.Add(new MailboxAddress(fromName, fromEmail));
				msg.To.Add(MailboxAddress.Parse(to));
				msg.Subject = "ReportMail 測試信件 (MailKit)";
				msg.Body = new BodyBuilder
				{
					HtmlBody = "<h3>這是一封 MailKit 測試信件</h3><p>如果你能看到，代表 Brevo SMTP 設定正確。</p>"
				}.ToMessageBody();

				using var client = new SmtpClient();
				client.Connect(host, port, SecureSocketOptions.StartTls);
				client.Authenticate(userName, password);

				client.Send(msg);
				client.Disconnect(true);

				return Ok($"✅ 測試信已送出到 {to} (使用 MailKit)");
			}
			catch (Exception ex)
			{
				return BadRequest($"❌ 發送失敗: {ex.Message}");
			}
		}
	}
}
