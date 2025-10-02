using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BookLoop.Services
{
	public interface IImageValidator
	{
		/// <summary>驗證上傳的檔案是否為合法圖片（大小、副檔名、MIME、magic bytes）</summary>
		Task<(bool IsValid, string? Error)> ValidateFileAsync(IFormFile? file);

		/// <summary>驗證圖片 URL（基本 URL 格式、http/https、檔名副檔名、可選 HEAD 檢查 content-type）</summary>
		Task<(bool IsValid, string? Error)> ValidateUrlAsync(string? url);
	}
}
