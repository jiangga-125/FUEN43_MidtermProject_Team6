using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;

namespace BookLoop.Controllers.api
{
	[ApiController]
	[Route("api/[controller]")]
	public class CategoriesController : ControllerBase
	{
		private readonly BookSystemContext _db;

		public CategoriesController(BookSystemContext db)
		{
			_db = db;
		}

		// GET: /api/categories
		// 若Program.cs 設定整站預設要驗證（FallbackPolicy），請保留 [AllowAnonymous]
		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> GetAll()
		{
			var items = await _db.Categories
				.AsNoTracking()
				.OrderBy(c => c.CategoryName)
				.Select(c => new {
					id = c.CategoryID,     // 你 model 用的是 CategoryID
					name = c.CategoryName,
					slug = c.Slug
				})
				.ToListAsync();

			return Ok(items);
		}

		// GET: /api/categories/with-count
		// (optional) 同時回傳每個分類的商品數量 — 注意：這會對 DB 做多個 Count 查詢，較大資料量時要優化
		[HttpGet("with-count")]
		[AllowAnonymous]
		public async Task<IActionResult> GetAllWithCount()
		{
			var q = _db.Categories
				.AsNoTracking()
				.OrderBy(c => c.CategoryName)
				.Select(c => new {
					id = c.CategoryID,
					name = c.CategoryName,
					slug = c.Slug,
					productCount = _db.Books.Count(b => b.CategoryID == c.CategoryID)
				});

			var items = await q.ToListAsync();
			return Ok(items);
		}
	}
}
