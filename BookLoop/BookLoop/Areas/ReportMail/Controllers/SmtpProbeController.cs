//直接產生 MailKit ProtocolLogger 的 log，幫助 debug 認證過程
using Microsoft.AspNetCore.Mvc;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class SmtpProbeController : Controller
	{
		private readonly IConfiguration _cfg;
		public SmtpProbeController(IConfiguration cfg) => _cfg = cfg;

		[HttpGet]
		public async Task<IActionResult> Run(int? port = null)
		{
			var s = _cfg.GetSection("Smtp");
			var host = s["Host"];
			var p = port ?? int.Parse(s["Port"] ?? "587");
			var user = s["UserName"];
			var pass = s["Password"];
			var from = s["FromEmail"];
			var to = from;

			string logText;
			string status;

			// 這裡直接用 File-based logger，寫到暫存檔，再讀回文字
			var tmpFile = Path.GetTempFileName();
			try
			{
				using var logger = new MailKit.ProtocolLogger(tmpFile);
				using var client = new MailKit.Net.Smtp.SmtpClient(logger);

				var opt = (p == 465) ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

				await client.ConnectAsync(host, p, opt);
				await client.AuthenticateAsync(user, pass);

				var msg = new MimeMessage();
				msg.From.Add(MailboxAddress.Parse(from));
				msg.To.Add(MailboxAddress.Parse(to));
				msg.Subject = "SMTP Probe OK";
				msg.Body = new TextPart("plain") { Text = "hello" };

				await client.SendAsync(msg);
				await client.DisconnectAsync(true);

				status = "OK";
			}
			catch (Exception ex)
			{
				status = "FAIL: " + ex.Message;
			}

			// 讀回完整 log
			logText = System.IO.File.ReadAllText(tmpFile, Encoding.UTF8);

			return Content(status + "\n\n" + logText, "text/plain; charset=utf-8");
		}
	}
}
