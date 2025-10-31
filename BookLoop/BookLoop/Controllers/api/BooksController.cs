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
			if (page < 1) page = 1;
			if (pageSize <= 0) pageSize = 20;

			var q = _db.Books
				.AsNoTracking()
				.Include(b => b.BookImages)
				.OrderBy(b => b.Title);

			var total = await q.CountAsync();
			var items = await q
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(b => new
				{
					id = b.BookID,
					title = b.Title,
					isbn = b.ISBN,
					slug = b.Slug,
					image = b.BookImages.Where(i => i.IsPrimary).Select(i => i.FilePath).FirstOrDefault(),
					listPrice = b.ListPrice,
					salePrice = b.SalePrice
				})
				.ToListAsync();

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
				images = b.BookImages.Select(i => new { i.ImageID, i.FilePath, i.IsPrimary })
			});
		}
	}
}
