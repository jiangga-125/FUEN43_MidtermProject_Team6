using System.Text.RegularExpressions;

namespace BookLoop.Services.Mail
{
	public class SimpleTemplateRenderer : ITemplateRenderer
	{
		private static readonly Regex Token = new(@"\{\{\s*(?<k>[A-Za-z0-9_.-]+)\s*\}\}",
			RegexOptions.Compiled);

		public string Render(string template, IDictionary<string, string> model)
		{
			if (string.IsNullOrEmpty(template)) return string.Empty;
			return Token.Replace(template, m =>
			{
				var k = m.Groups["k"].Value;
				return model.TryGetValue(k, out var v) ? v ?? "" : "";
			});
		}
	}
}
