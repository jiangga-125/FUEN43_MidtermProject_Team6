using Microsoft.AspNetCore.Mvc;
using BookLoop.Data;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
public class BookImagesController : ControllerBase
{
	private readonly BookSystemContext _db;
	private readonly IWebHostEnvironment _env;

	public BookImagesController(BookSystemContext db, IWebHostEnvironment env)
	{
		_db = db;
		_env = env;
	}

	// GET api/BookImages/book/{bookId}/cover
	[HttpGet("book/{bookId:int}/cover")]
	[AllowAnonymous]
	public IActionResult GetCoverByBookId(int bookId)
	{
		var img = _db.BookImages.FirstOrDefault(i => i.BookID == bookId && i.IsPrimary);
		if (img == null) return NotFound();

		return ServeImage(img.FilePath);
	}

	// GET api/BookImages/{imageId}/cover (若需要根據 imageId 取圖)
	[HttpGet("{imageId:int}/cover")]
	[AllowAnonymous]
	public IActionResult GetCoverByImageId(int imageId)
	{
		var img = _db.BookImages.FirstOrDefault(i => i.ImageID == imageId);
		if (img == null) return NotFound();

		return ServeImage(img.FilePath);
	}

	// helper：處理外部 url 或本地檔案回傳
	private IActionResult ServeImage(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath)) return NotFound();

		filePath = filePath.Trim();

		// 外部 URL -> 直接 Redirect（避免 proxy 大檔）
		if (filePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
		{
			return Redirect(filePath);
		}

		// 支援若 DB 存的是 /images/books/xxx.jpg 或只是檔名 xxx.jpg
		var cleaned = filePath.TrimStart('/', '\\');
		// 預設放在 wwwroot/images/books/{cleaned}
		var localPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "books", cleaned);

		if (!System.IO.File.Exists(localPath))
		{
			// 嘗試直接當作相對於 wwwroot 的路徑（例如 DB 可能存 images/...）
			var altPath = Path.Combine(_env.WebRootPath ?? "wwwroot", cleaned);
			if (System.IO.File.Exists(altPath))
			{
				localPath = altPath;
			}
			else
			{
				return NotFound();
			}
		}

		var contentType = GetContentType(localPath);

		// 設定 Cache-Control（可依需求調整）
		Response.Headers["Cache-Control"] = "public, max-age=604800"; // 7 days

		// 回傳實體檔案（FileStreamResult 會自動處理 stream）
		var fs = System.IO.File.OpenRead(localPath);
		return File(fs, contentType);
	}

	// 根據副檔名回 Content-Type，簡單實作
	private static string GetContentType(string path)
	{
		var ext = Path.GetExtension(path).ToLowerInvariant();
		return ext switch
		{
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".gif" => "image/gif",
			".webp" => "image/webp",
			".svg" => "image/svg+xml",
			".bmp" => "image/bmp",
			_ => "application/octet-stream"
		};
	}
}
