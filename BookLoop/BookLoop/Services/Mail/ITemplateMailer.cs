using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Services.Mail
{
	public interface ITemplateMailer
	{
		Task SendAsync(string templateKey, string? templateName, string to,
					   IDictionary<string, string> tokens, CancellationToken ct = default);
	}
}
