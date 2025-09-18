using System.Text.RegularExpressions;

namespace BookLoop.Helpers
{
	public static class SlugHelper
	{
		public static string Generate(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return Guid.NewGuid().ToString("N"); // 避免存空字串

			// 轉小寫（英文用得到，中文不影響）
			text = text.ToLowerInvariant();

			// 把全形空格（\u3000）換成半形空格，再統一轉成 "-"
			text = text.Replace('\u3000', ' ');
			text = Regex.Replace(text, @"\s+", "-");

			// 保留英文、數字、-、以及中文
			text = Regex.Replace(text, @"[^a-z0-9\-一-龥]", "");

			// 如果最後還是空 → fallback 用 Guid
			if (string.IsNullOrWhiteSpace(text))
				return Guid.NewGuid().ToString("N");

			return text;
		}
	}
}
