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
		private readonly ITemplateRenderer _renderer;  // 你原本的
		private readonly IMailService _mail;           // 你原本的

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

			await _mail.SendAsync(to, subject, body, ct); // ← 用你原本的寄信服務
		}
	}
}
