using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Services
{
	public class MailService
	{
		private readonly IConfiguration _config;
		public MailService(IConfiguration config) => _config = config;

		/// <summary>
		/// 寄送一般通知（無附件）
		/// </summary>
		public Task SendReportAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
		{
			return SendReportAsync(to, subject, body, null, null, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// 寄送郵件，可附加 Excel 等附件
		/// </summary>
		public async Task SendReportAsync(string to, string subject, string body,
											   string? attachmentName, byte[]? attachmentBytes,
											   string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
											   CancellationToken cancellationToken = default)
		{
			var smtp = _config.GetSection("Smtp");

			var host = smtp["Host"] ?? "smtp-relay.brevo.com";
			var port = int.Parse(smtp["Port"] ?? "587");
			var user = (smtp["UserName"] ?? "").Trim();   // Brevo Login (xxx@smtp-brevo.com)
			var pass = (smtp["Password"] ?? "").Trim();   // Brevo SMTP Key
			var fromEmail = (smtp["FromEmail"] ?? "").Trim(); // 已驗證的寄件位址
			var fromName = (smtp["FromName"] ?? "Report Mailer").Trim();

			if (fromEmail.EndsWith("@smtp-brevo.com", StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("FromEmail 不能是 @smtp-brevo.com，請改為 Brevo 後台已驗證的寄件位址。");

			var msg = new MimeMessage();
			msg.From.Add(new MailboxAddress(fromName, fromEmail));
			msg.To.Add(MailboxAddress.Parse(to));
			msg.Subject = subject;

			var builder = new BodyBuilder { HtmlBody = body };
			if (attachmentBytes is { Length: > 0 })
			{
				builder.Attachments.Add(
					string.IsNullOrWhiteSpace(attachmentName) ? "report.xlsx" : attachmentName,
					attachmentBytes,
					ContentType.Parse(contentType)
				);
			}
			msg.Body = builder.ToMessageBody();

			using var client = new SmtpClient();

			var options = (port == 465) ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
			await client.ConnectAsync(host, port, options, cancellationToken);

			// 防止 MailKit 嘗試 XOAUTH2 → Brevo 不支援
			client.AuthenticationMechanisms.Remove("XOAUTH2");

			await client.AuthenticateAsync(user, pass, cancellationToken);
			await client.SendAsync(msg, cancellationToken);
			await client.DisconnectAsync(true, cancellationToken);
		}
	}
}
