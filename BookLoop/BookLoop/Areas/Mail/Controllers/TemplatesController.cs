using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Mail.Controllers
{
	[Area("Mail")]
	public class TemplatesController : Controller
	{
		private readonly AppDbContext _db;
		public TemplatesController(AppDbContext db) => _db = db;

		// GET: /Mail/Templates
		public async Task<IActionResult> Index()
		{
			var list = await _db.Templates
				.Include(t => t.Versions)
				.OrderBy(t => t.TemplateKey)
				.ToListAsync();

			return View(list); 
		}

		// GET: /Mail/Templates/Create
		public IActionResult Create() => View();

		// POST: /Mail/Templates/Create
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("TemplateKey,Description")] Template m)
		{
			if (await _db.Templates.AnyAsync(x => x.TemplateKey == m.TemplateKey))
				ModelState.AddModelError(nameof(m.TemplateKey), "此 TemplateKey 已存在");
			if (!ModelState.IsValid) return View(m);

			_db.Templates.Add(m);
			await _db.SaveChangesAsync();
			return RedirectToAction("Index");
		}
	}

}
