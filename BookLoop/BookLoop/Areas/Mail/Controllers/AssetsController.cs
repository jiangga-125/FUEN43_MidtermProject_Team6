// Areas/Mail/Controllers/AssetsController.cs
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Areas.Mail.Controllers
{
	[Area("Mail")]
	public class AssetsController : Controller
	{
		private readonly IWebHostEnvironment _env;
		private readonly ILogger<AssetsController> _logger;
		public AssetsController(IWebHostEnvironment env, ILogger<AssetsController> logger)
		{
			_env = env; _logger = logger;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upload(IFormFile file)
		{
			if (file == null || file.Length == 0) return BadRequest("No file");

			var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "unlayer");
			Directory.CreateDirectory(uploads);
			var ext = Path.GetExtension(file.FileName);
			var name = $"{Guid.NewGuid():N}{ext}";
			var path = Path.Combine(uploads, name);

			await using (var fs = System.IO.File.Create(path))
				await file.CopyToAsync(fs);

			var baseUrl = $"{Request.Scheme}://{Request.Host}";
			var url = $"{baseUrl}/uploads/unlayer/{name}"; // 絕對網址（方便寄信顯示）
			return Json(new { url });
		}
	}
}
