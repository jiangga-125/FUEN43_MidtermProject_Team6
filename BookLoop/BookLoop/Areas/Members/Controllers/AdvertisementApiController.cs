using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace BookLoop.Controllers.Api
{	
	[ApiController]
	[Route("api/[controller]")]
	[AllowAnonymous]
	public class AdvertisementsApiController : ControllerBase
	{
		private readonly MemberContext _db;

		public AdvertisementsApiController(MemberContext db)
		{
			_db = db;
		}

		// ✅ 提供給前台 Vue 輪播使用的 API
		[HttpGet]
		public IActionResult GetActiveAds()
		{
			var now = DateTime.Now;

			var ads = _db.Advertisements
				.Where(a => a.IsActive &&                     // 只抓啟用的
							(!a.StartAt.HasValue || a.StartAt <= now) &&
							(!a.EndAt.HasValue || a.EndAt >= now))
				.OrderBy(a => a.DisplayOrder)
				.Select(a => new
				{
					id = a.AdvertisementID,
					title = a.Title,
					imageUrl = a.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
	? a.ImageUrl
	: $"{Request.Scheme}://{Request.Host}{(a.ImageUrl.StartsWith("/") ? "" : "/")}{a.ImageUrl}",

					linkUrl = a.LinkUrl
				})
				.ToList();

			return Ok(ads);
		}
	}
}
