using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Api
{
	[ApiController]
	[Route("api/[controller]")]
	public class BookImagesController : ControllerBase
	{
		private readonly BookSystemContext _db;
		private readonly IWebHostEnvironment _env;
		private static readonly HttpClient _http = new HttpClient();

		public BookImagesController(BookSystemContext db, IWebHostEnvironment env)
		{
			_db = db;
			_env = env;
		}

		// GET api/BookImages/{bookId}/cover
		[HttpGet("{bookId}/cover")]
		[AllowAnonymous] // 必須允許匿名，否則預設 FallbackPolicy 會攔截
		public async Task<IActionResult> GetCover(int bookId)
		{
			var img = await _db.BookImages
				.Where(bi => bi.BookID == bookId && bi.IsPrimary)
				.OrderByDescending(bi => bi.ImageID)
				.FirstOrDefaultAsync();

			if (img == null || string.IsNullOrWhiteSpace(img.FilePath))
				return NotFound();

			var path = img.FilePath.Trim();

			// 若是絕對 URL（外部圖片） -> 直接 redirect（讓 client 去抓）
			if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
			{
				// 可改用 Proxy 方式（下方有註解範例），但 redirect 最簡單也能保留瀏覽器的快取
				return Redirect(path);
			}

			// 處理本地檔名：支援存成「filename.jpg」或可能以 /images/books/... 的完整相對路徑
			// 正常情況下你在 Create/Update 是只存檔名，所以以 wwwroot/images/books 組成實體路徑
			string candidate;
			if (path.StartsWith("/"))
				candidate = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
			else
				candidate = Path.Combine(_env.WebRootPath, "images", "books", path);

			if (!System.IO.File.Exists(candidate))
				return NotFound();

			var provider = new FileExtensionContentTypeProvider();
			if (!provider.TryGetContentType(candidate, out var contentType))
				contentType = "application/octet-stream";

			// 設定快取（可依需求調整 max-age）
			Response.Headers["Cache-Control"] = "public,max-age=604800"; // 1 week

			var stream = System.IO.File.OpenRead(candidate);
			return File(stream, contentType);
		}

		/*
        // — 若你想要 proxy 外部圖片（不 redirect），可以用下面的範例（會把外部圖片抓回來並回傳 bytes）：
        private async Task<IActionResult> ProxyExternalImageAsync(string url)
        {
            try
            {
                using var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode);
                var contentType = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                Response.Headers["Cache-Control"] = "public,max-age=604800";
                return File(bytes, contentType);
            }
            catch
            {
                return StatusCode(502);
            }
        }
        */
	}
}
