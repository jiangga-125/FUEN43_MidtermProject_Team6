using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;


namespace BookLoop.Controllers.api
{
	[ApiController]
	[Route("api/[controller]")]
	public class BooksController : ControllerBase
	{
		private readonly BookSystemContext _db;

		public BooksController(BookSystemContext db)
		{
			_db = db;
		}

		// GET: /api/books?page=1&pageSize=20
		[HttpGet]
		[AllowAnonymous] // 公開給未登入前端
		public async Task<IActionResult> GetAll(int page = 1, int pageSize = 20)
		{
			string tab = "new";   // new / hot
			int? categoryId = null;  // 可選分類
			if (page < 1) page = 1;
			if (pageSize <= 0) pageSize = 20;

			var q = _db.Books
				.AsNoTracking()
				.Include(b => b.BookImages)
				.OrderBy(b => b.Title);

			// 篩選分類
			if (categoryId.HasValue)
			{
				q = (IOrderedQueryable<Models.Book>)q.Where(b => b.CategoryID == categoryId.Value);
			}

			// 篩選 tab
			switch (tab.ToLower())
			{
				case "new":
					q = q.OrderByDescending(b => b.CreatedAt);
					break;
				case "hot":
					// 這裡用 OrderDetails 數量當熱門依據
					q = q
						.Include(b => b.OrderDetails)
						.OrderByDescending(b => b.OrderDetails.Count);
					break;
				default:
					q = q.OrderBy(b => b.Title);
					break;
			}

			var total = await q.CountAsync();

			var rawItems = await q
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(b => new
				{
					id = b.BookID,
					title = b.Title,
					isbn = b.ISBN,
					slug = b.Slug,
					imageFilePath = b.BookImages.Where(i => i.IsPrimary).Select(i => i.FilePath).FirstOrDefault(),
					listPrice = b.ListPrice,
					salePrice = b.SalePrice
				})
				.ToListAsync();

			// 在記憶體組成 coverUrl（若 FilePath 以 http 開頭就直接使用，否則轉成絕對 URL 指向 wwwroot/images/books/{fileName}）
			var items = rawItems.Select(x =>
			{
				string? coverUrl = null;
				if (!string.IsNullOrWhiteSpace(x.imageFilePath) &&
			x.imageFilePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
				{
					coverUrl = x.imageFilePath;
				}
				else
				{
					coverUrl = $"{Request.Scheme}://{Request.Host}/api/BookImages/book/{x.id}/cover";
				}

				return new
				{
					id = x.id,
					title = x.title,
					isbn = x.isbn,
					slug = x.slug,
					coverUrl,
					listPrice = x.listPrice,
					salePrice = x.salePrice
				};
			}).ToList();


			return Ok(new { total, page, pageSize, items });
		}

		// GET: /api/books/{id}
		[HttpGet("{id:int}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetById(int id)
		{
			var b = await _db.Books
				.AsNoTracking()
				.Include(x => x.BookImages)
				.Include(x => x.Publisher)
				.Include(x => x.Category)
				.FirstOrDefaultAsync(x => x.BookID == id);

			if (b == null) return NotFound();

			var primary = b.BookImages.FirstOrDefault(i => i.IsPrimary);

			var coverUrl = $"{Request.Scheme}://{Request.Host}/api/BookImages/book/{b.BookID}/cover";

			return Ok(new
			{
				id = b.BookID,
				title = b.Title,
				isbn = b.ISBN,
				slug = b.Slug,
				description = b.Description,
				listPrice = b.ListPrice,
				salePrice = b.SalePrice,
				publisher = b.Publisher != null ? new { id = b.Publisher.PublisherID, name = b.Publisher.PublisherName } : null,
				category = b.Category != null ? new { id = b.Category.CategoryID, name = b.Category.CategoryName } : null,
				images = b.BookImages.Select(i => new { i.ImageID, i.FilePath, i.IsPrimary }),
				coverUrl,
				publishDate = b.PublishDate.HasValue ? b.PublishDate.Value.ToString("yyyy-MM-dd") : null
			});
		}
	}
}
