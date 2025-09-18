using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BookLoop.Services
{
	public class ImageValidator : IImageValidator
	{
		private readonly ImageValidationOptions _opts;
		private readonly HttpClient _httpClient;

		public ImageValidator(IOptions<ImageValidationOptions> opts, HttpClient httpClient)
		{
			_opts = opts.Value;
			_httpClient = httpClient;
		}

		public async Task<(bool IsValid, string? Error)> ValidateFileAsync(IFormFile? file)
		{
			if (file == null || file.Length == 0)
				return (false, "沒有選擇任何檔案。");

			if (file.Length > _opts.MaxFileBytes)
				return (false, $"檔案太大，請上傳小於 {_opts.MaxFileBytes / (1024 * 1024)} MB 的圖片。");

			var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
			if (string.IsNullOrEmpty(ext) || !_opts.PermittedExtensions.Contains(ext))
				return (false, "不支援的圖片格式；只接受 JPG/PNG/GIF/WEBP。");

			// 檢查 MIME
			if (string.IsNullOrEmpty(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				return (false, "上傳檔案的 MIME 類型看起來不是圖片。");

			// 讀前幾個 bytes 檢查 magic bytes（更可靠）
			try
			{
				using var stream = file.OpenReadStream();
				var header = new byte[12];
				var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
				if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

				if (read >= 4)
				{
					// PNG: 89 50 4E 47
					if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
						return (true, null);

					// JPG: FF D8
					if (header[0] == 0xFF && header[1] == 0xD8)
						return (true, null);

					// GIF: "GIF8"
					if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
						return (true, null);

					// WEBP: "RIFF" ... "WEBP"
					if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
						return (true, null);
				}

				// 若 magic bytes 無法明確判斷，視為不安全（可視需求放寬）
				return (false, "上傳檔案不是受支援的圖片格式（magic bytes 檢查失敗）。");
			}
			catch
			{
				return (false, "無法讀取上傳檔案以進行驗證。");
			}
		}

		public async Task<(bool IsValid, string? Error)> ValidateUrlAsync(string? url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return (false, "URL 為空。");

			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
				return (false, "URL 格式錯誤。");

			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
				return (false, "URL 必須使用 http 或 https。");

			var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
			if (!string.IsNullOrEmpty(ext) && !_opts.PermittedExtensions.Contains(ext))
				return (false, "URL 的檔案副檔名看起來不是受支援的圖片格式。");

			// 嘗試發 HEAD 請求檢查 Content-Type（非必要，但能多一層驗證）
			try
			{
				using var req = new HttpRequestMessage(HttpMethod.Head, uri);
				var res = await _httpClient.SendAsync(req);
				if (res.IsSuccessStatusCode)
				{
					if (res.Content.Headers.ContentType != null)
					{
						var ct = res.Content.Headers.ContentType.MediaType;
						if (!ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
							return (false, "外部 URL 的 Content-Type 不是 image/*。");
					}
				}
				// 如果 HEAD 無法成功（有些伺服器不允許），還是允許以副檔名判斷
			}
			catch
			{
				// 忽略網路錯誤（可能是 CORS/伺服器不支援 HEAD），只依副檔名判斷
			}

			return (true, null);
		}
	}
}
