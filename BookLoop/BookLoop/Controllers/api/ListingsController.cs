using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using BookLoop.Data;

namespace BookLoop.Controllers.api
{
	[ApiController]
	[Route("api/[controller]")]
	public class ListingsController : ControllerBase
	{
		private readonly BorrowContext _db;
		public ListingsController(BorrowContext db) { _db = db; }

		// GET: /api/listings?page=1&pageSize=20
		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> GetAll(int page = 1, int pageSize = 20)
		{
			if (page < 1) page = 1;
			if (pageSize <= 0) pageSize = 20;

			var q = _db.Listings
				.AsNoTracking()
				.Include(l => l.ListingImages)
				.Include(l => l.Publisher)
				.Include(l => l.Category)
				.OrderByDescending(l => l.CreatedAt);

			var total = await q.CountAsync();
			var items = await q
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(l => new
				{
					id = l.ListingID,
					title = l.Title,
					isbn = l.ISBN,
					condition = l.Condition,
					status = l.Status,
					createdAt = l.CreatedAt,
					image = l.ListingImages
								.OrderByDescending(img => img.ImageID)
								.Select(img => img.ImageUrl)
								.FirstOrDefault(),
					category = l.Category != null ? new { id = l.Category.CategoryID, name = l.Category.CategoryName } : null
				})
				.ToListAsync();

			return Ok(new { total, page, pageSize, items });
		}

		// GET: /api/listings/{id}
		[HttpGet("{id:int}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetById(int id)
		{
			var l = await _db.Listings
				.AsNoTracking()
				.Include(x => x.ListingImages)
				.Include(x => x.Publisher)
				.Include(x => x.Category)
				.FirstOrDefaultAsync(x => x.ListingID == id);

			if (l == null) return NotFound();

			return Ok(new
			{
				id = l.ListingID,
				title = l.Title,
				isbn = l.ISBN,
				condition = l.Condition,
				status = l.Status,
				createdAt = l.CreatedAt,
				images = l.ListingImages
						  .OrderBy(img => img.ImageID)
						  .Select(img => new { img.ImageID, url = img.ImageUrl, img.Caption })
						  .ToList(),
				category = l.Category != null ? new { id = l.Category.CategoryID, name = l.Category.CategoryName } : null
			});
		}
	}
}
