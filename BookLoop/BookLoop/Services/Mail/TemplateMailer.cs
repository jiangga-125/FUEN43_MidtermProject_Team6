using BookLoop.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    jobRecipientId: null,
    category: "System",
    cancellationToken: ct);
        }

        //供系統信使用
        public async Task SendAsync(string templateKey, string to, object vars, CancellationToken ct = default)
        {
            var dict = ToDict(vars);
            await SendAsync(templateKey, templateName: null, to: to, tokens: dict, ct);
        }

        private static IDictionary<string, string> ToDict(object? vars)
        {
            if (vars == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (vars is IDictionary<string, string> d1)
                return new Dictionary<string, string>(d1, StringComparer.OrdinalIgnoreCase);

            if (vars is IEnumerable<KeyValuePair<string, string>> kvs)
                return kvs.ToDictionary(k => k.Key, k => k.Value ?? "", StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var props = vars.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                var val = p.GetValue(vars);
                map[p.Name] = val?.ToString() ?? "";
            }
            return map;
        }
    }
}
