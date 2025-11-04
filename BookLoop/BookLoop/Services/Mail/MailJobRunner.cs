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
    public class MailJobRunner : IMailJobRunner
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
            if (job == null) { _logger.LogWarning("MailJob {JobId} not found.", jobId); return; }

            // 已完成/取消就跳過
            if (job.Status is "Completed" or "Canceled")
            {
                _logger.LogInformation("MailJob {JobId} status is {Status}, skip.", jobId, job.Status);
                return;
            }

            // 起始
            job.Status = "Sending";                     // ← 改你的狀態字串
            job.StartedAt = DateTime.Now;
            await _db.SaveChangesAsync(ct);

            try
            {
                // 讀模板版本（沿用你原本的渲染邏輯）
                var version = await _db.TemplateVersions
                    .Include(v => v.Template)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v =>
                        v.TemplateId == job.TemplateId &&
                        v.TemplateVersionId == job.TemplateVersionId, ct);
                if (version == null)
                    throw new InvalidOperationException($"找不到 TemplateVersion (TemplateId={job.TemplateId}, TemplateVersionId={job.TemplateVersionId})");

                // 取名單：只跑 Pending（你可自行換成 Pending+Failed 做重送）
                var recipients = await _db.MailJobRecipients
                    .Where(r => r.MailJobId == job.JobId && r.Status == "Pending")
                    .OrderBy(r => r.MailJobRecipientId)
                    .ToListAsync(ct);

                if (recipients.Count == 0)
                    throw new InvalidOperationException("沒有可處理的收件名單（Pending）。");

                var fail = 0;

                foreach (var r in recipients)
                {
                    ct.ThrowIfCancellationRequested();

                    // 置換 tokens（你原本就用 SimpleTemplateRenderer）
                    var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Recipient"] = r.RecipientEmail,
                        ["Name"] = r.RecipientName ?? "",
                        ["Campaign"] = job.CampaignName ?? "",
                        ["TemplateKey"] = job.TemplateKey ?? ""
                    };
                    var subject = _renderer.Render(version.Subject ?? "", tokens);
                    var body = _renderer.Render(version.BodyHtml ?? "", tokens);

                    try
                    {
                        // 寄送：把 JobRecipientId 帶進去，日誌就能 1:1 對到這位名單
                        await _mail.SendAsync(
                            to: r.RecipientEmail,
                            subject: subject,
                            body: body,
                            attachmentName: null,
                            attachmentBytes: null,
                            contentType: "text/html",
                            templateId: version.TemplateId,
                            templateKey: version.Template?.TemplateKey,
                            templateVersionId: version.TemplateVersionId,
                            mailJobId: job.JobId,
                            jobRecipientId: r.MailJobRecipientId,      // ← 關鍵
                            category: "Bulk",
                            cancellationToken: ct);

                        r.Status = "Sent";
                        r.SentAt = DateTime.Now;
                        job.SentCount++;
                        await _db.SaveChangesAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        r.Status = "Failed";
                        r.Error = ex.Message;
                        fail++;
                        await _db.SaveChangesAsync(ct);
                        _logger.LogError(ex, "Job {JobId} 收件者 {Email} 寄送失敗", job.JobId, r.RecipientEmail);
                    }

                    // 節流（維持你原本概念，可抽到設定）
                    await Task.Delay(150, ct);
                }

                job.FinishedAt = DateTime.Now;
                job.Status = /* 你可以選擇嚴格或寬鬆 */ "Completed";
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("MailJob {JobId} done. Sent {Ok}/{Total}, Failed={Fail}.",
                    job.JobId, job.SentCount, job.TotalRecipients, fail);
            }
            catch (OperationCanceledException)
            {
                job.Status = "Canceled";
                await _db.SaveChangesAsync(CancellationToken.None);
                _logger.LogWarning("MailJob {JobId} cancelled.", job.JobId);
                throw;
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Description = (job.Description ?? string.Empty) + $" | Error: {ex.Message}";
                await _db.SaveChangesAsync(ct);
                _logger.LogError(ex, "MailJob {JobId} failed.", job.JobId);
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
