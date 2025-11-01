using Azure;
using BookLoop.Data;
using BookLoop.Models; 
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Services.Mail
{
	/// <summary>
	/// MailService 寄信服務：實作 IMailService 介面。
	/// </summary>
	public class MailService : IMailService
	{
		private readonly IConfiguration _config;
        private readonly ILogger<MailService>? _logger;
        private readonly IWebHostEnvironment? _env;
        private readonly AppDbContext _db;
        public MailService(
            IConfiguration config,
            ILogger<MailService>? logger = null,
            IWebHostEnvironment? env = null,
            AppDbContext? db = null)
        {
            _config = config;
            _logger = logger;
            _env = env;
            _db = db!;
        }

        /// <summary>
        /// 寄送一般通知（無附件）
        /// </summary>
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
                    => SendAsync(to, subject, body, attachmentName: null, attachmentBytes: null, contentType: "application/octet-stream", cancellationToken);

        /// <summary>
        /// 寄送郵件，可附加附件（Excel、PDF 等）
        /// </summary>
        public Task SendAsync(
            string to,
            string subject,
            string body,
            string? attachmentName,
            byte[]? attachmentBytes,
            string contentType = "application/octet-stream",
            CancellationToken cancellationToken = default)
            => SendAsync(
                to, subject, body,
                attachmentName, attachmentBytes, contentType,
                templateId: null, templateKey: null, templateVersionId: null, mailJobId: null,
                category: "System",
                cancellationToken);

        /// <summary>
        /// 寄送郵件，可附加附件（Excel、PDF 等）
        /// 儲存來源模板及是否群發批次資訊，並分類用途
        /// </summary>
        public async Task SendAsync(
            string to,
            string subject,
            string body,
            string? attachmentName,
            byte[]? attachmentBytes,
            string contentType,
            int? templateId,
            string? templateKey,
            int? templateVersionId,
            long? mailJobId,
            string category,
            CancellationToken cancellationToken = default)
        {
            // 讀 SMTP 設定
            var smtp = _config.GetSection("Smtp");
            var host = smtp["Host"] ?? "smtp-relay.brevo.com";
            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var user = (smtp["UserName"] ?? smtp["User"] ?? "").Trim();
            var pass = (smtp["Password"] ?? "").Trim();
            var fromEmail = (smtp["FromEmail"] ?? "").Trim();
            var fromName = (smtp["FromName"] ?? "BookLoop").Trim();

            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("Smtp:FromEmail 未設定。");

            // 組 MimeMessage
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject ?? string.Empty;

            var builder = new BodyBuilder();

            // 影像策略：Cid（離線也能顯示）或 Hosted（你有 PublicBaseUrl）
            var imageMode = (smtp["ImageEmbedding"] ?? "Cid").Trim();
            var publicBaseUrl = (smtp["PublicBaseUrl"] ?? "").Trim();

            string html = body ?? string.Empty;
            if (string.Equals(imageMode, "Hosted", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(publicBaseUrl))
                html = RewriteRelativeImgToAbsolute(html, publicBaseUrl);
            else
                html = EmbedLocalImagesToCid(html, builder); // 預設：Cid

            builder.HtmlBody = html;

            if (attachmentBytes is { Length: > 0 })
            {
                var name = string.IsNullOrWhiteSpace(attachmentName) ? "report.xlsx" : attachmentName!;
                builder.Attachments.Add(name, attachmentBytes, ContentType.Parse(contentType));
            }

            message.Body = builder.ToMessageBody();

            // === 先寫 Pending Log ===
            var log = new MailSendLog
            {
                Category = string.IsNullOrWhiteSpace(category) ? "System" : category,
                TemplateId = templateId,
                TemplateKey = templateKey ?? "",
                TemplateVersionId = templateVersionId,
                MailJobId = mailJobId,
                Recipient = to,
                Subject = subject ?? "",
                Status = "Pending",
                SentAt = DateTime.Now,
                BodySnapshot = body
            };

            _db.MailSendLogs.Add(log);                 // ← 使用複數 DbSet 名稱
            await _db.SaveChangesAsync(cancellationToken);

            // === 寄送 ===
            try
            {
                using var client = new SmtpClient();
                var secure = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(host, port, secure, cancellationToken);
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                if (!string.IsNullOrEmpty(user))
                    await client.AuthenticateAsync(user, pass, cancellationToken);

                var response = await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                // 成功
                log.TemplateVersionId = templateVersionId;
                log.Status = "Sent";
                log.ProviderMsgId = response;
                log.BodySnapshot = body;
                log.Error = null;
                log.SentAt = DateTime.Now;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // 失敗
                log.TemplateVersionId = templateVersionId;
                log.BodySnapshot = body;
                log.Status = "Failed";
                log.Error = ex.Message;
                await _db.SaveChangesAsync(cancellationToken);

                _logger?.LogError(ex, "Mail send failed to {To}", to);
                throw;
            }
        }
        // ====== 圖片處理（Hosted / Cid）======

        // Hosted：把 /uploads/... 改成 https://公開端點/uploads/...
        private string RewriteRelativeImgToAbsolute(string html, string publicBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(publicBaseUrl)) return html;
            publicBaseUrl = publicBaseUrl.TrimEnd('/');

            return Regex.Replace(
                html,
                "src=[\"']\\s*(/uploads/[^\"']+)[\"']",
                m => $"src=\"{publicBaseUrl}{m.Groups[1].Value}\"",
                RegexOptions.IgnoreCase);
        }

        // Cid：把本機/localhost/相對路徑圖轉成 cid，並加入 LinkedResources（同一張圖去重）
        private static readonly Regex ImgSrcRegex =
            new Regex("<img\\s+[^>]*?src=[\"'](?<src>.*?)[\"'][^>]*?>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private string EmbedLocalImagesToCid(string html, BodyBuilder builder)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            // 同封信「去重」：相同檔案只嵌一次
            var cache = new Dictionary<string, MimeEntity>(StringComparer.OrdinalIgnoreCase);

            return ImgSrcRegex.Replace(html, match =>
            {
                var tag = match.Value;
                var src = match.Groups["src"].Value;

                // data: 或 外部 https（非 localhost）→ 放過
                if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return tag;
                if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (!src.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                        return tag;
                }

                // 轉實體路徑
                var path = ResolveToWebRootPath(src);
                if (!File.Exists(path)) return tag;

                // 去重
                if (cache.TryGetValue(path, out var existed))
                {
                    var cid0 = ((MimePart)existed).ContentId;
                    return Regex.Replace(tag, "src=[\"'].*?[\"']", $"src=\"cid:{cid0}\"", RegexOptions.IgnoreCase);
                }

                var res = builder.LinkedResources.Add(path);
                res.ContentId = MimeUtils.GenerateMessageId(); // Content-Id
                res.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                res.ContentType.Name = Path.GetFileName(path);
                cache[path] = res;

                return Regex.Replace(tag, "src=[\"'].*?[\"']", $"src=\"cid:{res.ContentId}\"", RegexOptions.IgnoreCase);
            });
        }

        // 把 /uploads/... 或 https://localhost:xxxx/uploads/... 轉成 wwwroot 實體路徑
        private string ResolveToWebRootPath(string src)
        {
            var webroot = _env?.WebRootPath ?? "wwwroot";

            // 1) 相對路徑（/uploads/...）
            var rel = src.TrimStart('~').TrimStart('/');
            var path = Path.Combine(webroot, rel);
            if (File.Exists(path)) return path;

            // 2) 完整網址中截取 /uploads/ 之後
            var idx = src.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var fromUploads = src.Substring(idx + 1); // 去掉第一個 '/'
                path = Path.Combine(webroot, fromUploads);
            }
            return path;
        }


    }
}
