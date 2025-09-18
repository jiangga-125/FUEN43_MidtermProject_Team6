namespace BookLoop.Services
{
	public class ImageValidationOptions
	{
		/// <summary>允許的副檔名（小寫，包含點，例如 ".jpg"）</summary>
		public string[] PermittedExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

		/// <summary>最大允許檔案大小 (bytes)。預設 5 MB</summary>
		public long MaxFileBytes { get; set; } = 5 * 1024 * 1024;
	}
}
