using System.Net;
using System.Net.Mail;

namespace ReportMail.Services
{
	public class MailService
	{
		private readonly IConfiguration _config;
		public MailService(IConfiguration config) => _config = config;

		public void SendReport(string to, string subject, string body, string filePath)
		{
			var smtp = _config.GetSection("Smtp");

			using var client = new SmtpClient(smtp["Host"], int.Parse(smtp["Port"]))
			{
				Credentials = new NetworkCredential(
					smtp["UserName"],  // Login (smtp-brevo.com)
					smtp["Password"]   // SMTP Key
				),
				EnableSsl = bool.Parse(smtp["EnableSsl"])
			};

			using var message = new MailMessage();
			// 注意：這裡用 FromEmail（你在 Senders 驗證過的 Gmail）
			message.From = new MailAddress(smtp["FromEmail"], smtp["FromName"]);
			message.To.Add(to);
			message.Subject = subject;
			message.Body = body;

			if (!string.IsNullOrEmpty(filePath))
				message.Attachments.Add(new Attachment(filePath));

			client.Send(message);
		}
	}
}
