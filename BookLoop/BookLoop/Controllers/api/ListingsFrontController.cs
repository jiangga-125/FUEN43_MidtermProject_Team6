using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers.api
{
    [Route("api/listings")]
    [ApiController]
	[AllowAnonymous]
	public class ListingsFrontController : ControllerBase
    {
        private readonly BorrowContext _context;
        public ListingsFrontController(BorrowContext context)
        {
            _context = context;
        }
        [HttpGet("front")] // GET /api/listings/front
        public async Task<ActionResult<IEnumerable<ListingFrontDto>>> GetListingsFront()
        {
            var listings = await _context.Listings.AsNoTracking().OrderByDescending(x => x.CategoryID)
                .Select(x => new ListingFrontDto
                {
                    ListingId = x.ListingID,
                    CategoryId = x.CategoryID,
                    PublisherId = x.PublisherID,
                    Title = x.Title,
                    ISBN = x.ISBN,
                    Condition = x.Condition,
                    Status = x.Status,
                    PublisherName = x.Publisher.PublisherName,
                    CategoryName = x.Category.CategoryName,
                    AuthorName = x.ListingAuthors
                        .Select(la => la.AuthorName)
                        .FirstOrDefault() ?? "未知作者",
                    ImageUrl = x.ListingImages.Select(li => li.ImageUrl).FirstOrDefault(),

                }).ToListAsync();
            
            
            return Ok(listings);
        }
    }
}
