using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoop.Services.Mail
{
    /// <summary>
    /// 執行一次 MailJob（群發或排程，取決於建立 Job 時的 SendAt）。
    /// 流程：讀取模板版本 → 解析名單 → 逐筆渲染 → 寄送 → 更新狀態。
    /// </summary>
    public class MailJobRunner
    {
        private readonly AppDbContext _db;
        private readonly IMailService _mail;
        private readonly ITemplateRenderer _renderer;
        private readonly ILogger<MailJobRunner> _logger;

        public MailJobRunner(
            AppDbContext db,
            IMailService mail,
            ITemplateRenderer renderer,
            ILogger<MailJobRunner> logger)
        {
            _db = db;
            _mail = mail;
            _renderer = renderer;
            _logger = logger;
        }

        /// <summary>
        /// 被排程系統（例如 Hangfire）呼叫進來。
        /// </summary>
        public async Task RunAsync(long jobId, CancellationToken ct = default)
        {
            var job = await _db.MailJobs.FirstOrDefaultAsync(x => x.JobId == jobId, ct);
            if (job == null)
            {
                _logger.LogWarning("MailJob {JobId} not found.", jobId);
                return;
            }

            // 若已完成或取消就不再執行
            if (job.Status is "Done" or "Cancelled")
            {
                _logger.LogInformation("MailJob {JobId} status is {Status}, skip.", jobId, job.Status);
                return;
            }

            job.Status = "Running";
            await _db.SaveChangesAsync(ct);

            try
            {
                // 1) 讀取模板 & 指定版本（你目前的模型 TemplateVersionId 是必填 int）
                var version = await _db.TemplateVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v =>
                        v.TemplateId == job.TemplateId &&
                        v.TemplateVersionId == job.TemplateVersionId, ct);

                if (version == null)
                    throw new InvalidOperationException($"找不到 TemplateVersion (TemplateId={job.TemplateId}, TemplateVersionId={job.TemplateVersionId})");

                // 2) 解析名單（MVP：SegmentQuery = CSV，每行：email[,name]）
                var recipients = ParseCsvRecipients(job.SegmentQuery);
                if (recipients.Count == 0)
                    throw new InvalidOperationException("SegmentQuery 解析後沒有任何收件者。");

                // 3) 逐筆渲染 + 寄送
                foreach (var (email, name) in recipients)
                {
                    ct.ThrowIfCancellationRequested();

                    // 簡單 tokens（之後要加更多欄位很容易）
                    var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Recipient"] = email,
                        ["Name"] = name ?? "",
                        ["Campaign"] = job.CampaignName ?? "",
                        ["TemplateKey"] = job.TemplateKey ?? ""
                    };

                    var subject = _renderer.Render(version.Subject ?? string.Empty, tokens);
                    var body = _renderer.Render(version.BodyHtml ?? string.Empty, tokens);

                    // 你的 MailService 會寫 MailSendLog（若要把 JobId 帶進 Log，之後可擴充 MailService 的多載）
                    await _mail.SendAsync(email, subject, body, ct);

                    // MVP 簡單節流，避免打爆供應商速率（之後可改設定）
                    await Task.Delay(150, ct);
                }

                job.Status = "Done";
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("MailJob {JobId} succeeded. Sent {Count} mails.", jobId, recipients.Count);
            }
            catch (OperationCanceledException)
            {
                job.Status = "Cancelled";
                await _db.SaveChangesAsync(CancellationToken.None);
                _logger.LogWarning("MailJob {JobId} cancelled.", jobId);
                throw;
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Description = (job.Description ?? string.Empty) + $" | Error: {ex.Message}";
                await _db.SaveChangesAsync(ct);
                _logger.LogError(ex, "MailJob {JobId} failed.", jobId);
                throw;
            }
        }

        /// <summary>
        /// 解析 CSV 名單（每行：email[,name]），忽略空白行。
        /// </summary>
        private static List<(string email, string? name)> ParseCsvRecipients(string? csv)
        {
            var result = new List<(string, string?)>();
            if (string.IsNullOrWhiteSpace(csv)) return result;

            using var sr = new StringReader(csv);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;

                var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
                var email = parts[0];
                var name = parts.Length > 1 ? parts[1] : null;

                // 這裡不做嚴格 email 驗證，留給後續擴充
                result.Add((email, name));
            }
            return result;
        }
    }
}
