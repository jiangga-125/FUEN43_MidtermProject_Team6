using BookLoop.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Services.Mail
{
	public class TemplateMailer : ITemplateMailer
	{
		private readonly AppDbContext _db;
		private readonly ITemplateRenderer _renderer;  
		private readonly IMailService _mail;           

		public TemplateMailer(AppDbContext db, ITemplateRenderer renderer, IMailService mail)
		{ _db = db; _renderer = renderer; _mail = mail; }

		public async Task SendAsync(string templateKey, string? templateName, string to,
									IDictionary<string, string> tokens, CancellationToken ct = default)
		{
			var q = _db.TemplateVersions
					   .Include(v => v.Template)
					   .Where(v => v.Template.TemplateKey == templateKey && v.IsActive);

			var version = !string.IsNullOrWhiteSpace(templateName)
				? await q.FirstOrDefaultAsync(v => v.TemplateName == templateName, ct)
				: (await q.FirstOrDefaultAsync(v => v.IsDefault, ct))
				  ?? await q.OrderByDescending(v => v.UpdatedAt).FirstOrDefaultAsync(ct);

			if (version == null)
				throw new InvalidOperationException($"找不到可用模板：Key={templateKey}, Name={templateName ?? "(未指定)"}");

			var subject = _renderer.Render(version.Subject ?? "", tokens);
			var body = _renderer.Render(version.BodyHtml ?? "", tokens);

            await _mail.SendAsync(
    to: to,
    subject: subject,
    body: body,
    attachmentName: null,
    attachmentBytes: null,
    contentType: "application/octet-stream",
    templateId: version.TemplateId,
    templateKey: version.Template.TemplateKey,
    templateVersionId: version.TemplateVersionId,
    mailJobId: null,             // 群發時請由控制器傳入 JobId
    category: "System",
    cancellationToken: ct);
        }
	}
}
