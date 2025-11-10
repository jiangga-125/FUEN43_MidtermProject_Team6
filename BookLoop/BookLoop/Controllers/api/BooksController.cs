using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;


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
		public async Task<IActionResult> GetAll(int page = 1, int pageSize = 20, string tab = "new", int? categoryId = null)
		{
			if (page < 1) page = 1;
			if (pageSize <= 0) pageSize = 20;

			var q = _db.Books
				.AsNoTracking()
				.Include(b => b.BookImages)
				.AsQueryable();

			// 篩選分類（從 query 讀入）
			if (categoryId.HasValue)
			{
				q = q.Where(b => b.CategoryID == categoryId.Value);
			}

			// 篩選 tab（new / hot）
			switch (tab?.ToLower())
			{
				case "new":
					q = q.OrderByDescending(b => b.CreatedAt);
					break;
				case "hot":
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
				.Include(x => x.Author)
				.Include(x => x.Inventories)
					.ThenInclude(inv => inv.Branch)
				.FirstOrDefaultAsync(x => x.BookID == id);

			if (b == null) return NotFound();

			var primary = b.BookImages.FirstOrDefault(i => i.IsPrimary);

			var coverUrl = $"{Request.Scheme}://{Request.Host}/api/BookImages/book/{b.BookID}/cover";

			var authors = new List<object>();
			if (b.Author != null)
			{
				authors.Add(new { id = b.Author.AuthorID, name = b.Author.AuthorName });
			}

			var invByBranch = (b.Inventories ?? Enumerable.Empty<BookInventory>())
			.Select(inv => new
			{
				branchId = inv.BranchID,
				branchName = inv.Branch != null ? inv.Branch.BranchName : null,
				onHand = inv.OnHand,
				reserved = inv.Reserved,
				available = inv.OnHand - inv.Reserved,
				updatedAt = inv.UpdatedAt
			})
			.ToList();

			var totalAvailable = invByBranch.Select(x => x.available).DefaultIfEmpty(0).Sum();

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
				publishDate = b.PublishDate.HasValue ? b.PublishDate.Value.ToString("yyyy-MM-dd") : null,
				authors = authors,
				inventory = new
				{
					total = totalAvailable,
					byBranch = invByBranch
				}
			});
		}

		// GET: /api/books/related/{id}?limit=8
		[HttpGet("related/{id:int}")]
		[AllowAnonymous]
		public async Task<IActionResult> Related(int id, int limit = 8)
		{
			var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.BookID == id);
			if (book == null) return NotFound();

			var q = _db.Books
				.AsNoTracking()
				.Include(b => b.BookImages)
				.Where(b => b.CategoryID == book.CategoryID && b.BookID != id)
				.OrderByDescending(b => b.SalePrice) // 這裡示範一個排序（可改成 OrderDetails.Count 或其他）
				.Take(limit)
				.Select(b => new
				{
					id = b.BookID,
					title = b.Title,
					imageFilePath = b.BookImages.Where(i => i.IsPrimary).Select(i => i.FilePath).FirstOrDefault(),
					listPrice = b.ListPrice,
					salePrice = b.SalePrice
				});

			var raw = await q.ToListAsync();
			var items = raw.Select(x =>
			{
				string? coverUrl = null;
				if (!string.IsNullOrWhiteSpace(x.imageFilePath) && x.imageFilePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
					coverUrl = x.imageFilePath;
				else
					coverUrl = $"{Request.Scheme}://{Request.Host}/api/BookImages/book/{x.id}/cover";

				return new { id = x.id, title = x.title, coverUrl, listPrice = x.listPrice, salePrice = x.salePrice };
			}).ToList();

			return Ok(new { items });
		}

		// GET: /api/books/random?count=3
		[HttpGet("random")]
		[AllowAnonymous]
		public async Task<IActionResult> Random(int count = 3)
		{
			// 注意：OrderBy(Guid.NewGuid()) 在 EF 會轉成 SQL ORDER BY NEWID()
			var q = await _db.Books
				.AsNoTracking()
				.Include(b => b.BookImages)
				.OrderBy(b => Guid.NewGuid())
				.Take(count)
				.Select(b => new
				{
					id = b.BookID,
					title = b.Title,
					imageFilePath = b.BookImages.Where(i => i.IsPrimary).Select(i => i.FilePath).FirstOrDefault(),
					listPrice = b.ListPrice,
					salePrice = b.SalePrice
				})
				.ToListAsync();

			var items = q.Select(x =>
			{
				string? coverUrl = null;
				if (!string.IsNullOrWhiteSpace(x.imageFilePath) && x.imageFilePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
					coverUrl = x.imageFilePath;
				else
					coverUrl = $"{Request.Scheme}://{Request.Host}/api/BookImages/book/{x.id}/cover";

				return new { id = x.id, title = x.title, coverUrl, listPrice = x.listPrice, salePrice = x.salePrice };
			}).ToList();

			return Ok(new { items });
		}
	}
}
