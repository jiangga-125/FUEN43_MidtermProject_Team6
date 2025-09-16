using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Shop;
using BookLoop.Data.Contexts; // ReportMailDbContext

namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	// TODO: 權限就緒後改回這行，並移除 Index 上的 [AllowAnonymous]
	// [Authorize(Roles = "Admin,Marketing")]
	[AllowAnonymous]
	public class ReportsAdminController : Controller
	{
		private readonly ShopDbContext _shop;
		private readonly ReportMailDbContext _db;

		public ReportsAdminController(ShopDbContext shop, ReportMailDbContext db)
		{ _shop = shop; _db = db; }

		/// <summary>管理者總覽頁：與 Reports/Index 同版型，但可切 Publisher。</summary>
		[HttpGet]
		[AllowAnonymous] // 權限未接好前暫開
		public async Task<IActionResult> Index()
		{
			// 不假設一定有 Publishers 表，從 Books/Listings 取 PublisherID
			var idsFromBooks = await _shop.Books.AsNoTracking().Select(b => (int?)b.PublisherID).Distinct().ToListAsync();
			var idsFromListings = await _shop.Listings.AsNoTracking().Select(l => (int?)l.PublisherID).Distinct().ToListAsync();
			var allIds = idsFromBooks.Concat(idsFromListings).Where(id => id.HasValue).Select(id => id!.Value).Distinct().OrderBy(x => x).ToList();

			ViewBag.PublisherIds = allIds; // e.g. [1,2,3,...]
			return View();
		}

		/// <summary>
		/// 依 Publisher 回傳該書商的自訂報表清單（分圖表類型）。category = line|bar|pie
		/// 規則：ReportDefinitions.IsActive=1，且其 ReportFilters 內若有 Publisher 篩選，須包含此 publisherId；
		/// 若該定義沒有任何 Publisher 篩選，視為「全域可用」，也一併列出。
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> Definitions(string category, int? publisherId)
		{
			var cat = (category ?? "").Trim().ToLower();
			var defs = await _db.ReportDefinitions.AsNoTracking()
				.Where(r => r.IsActive && (string.IsNullOrEmpty(cat) || (r.Category != null && r.Category.ToLower() == cat)))
				.Select(r => new { r.ReportDefinitionID, r.ReportName })
				.ToListAsync();

			// 取出所有含 Publisher 篩選的定義（ValueJson 可能是 ["1","2"] 或 [1,2]）
			var pubFilt = await _db.ReportFilters.AsNoTracking()
				.Where(f => f.FieldName != null && (
					 f.FieldName == "PublisherID" || f.FieldName == "publisherId" || f.FieldName == "publisher"))
				.Select(f => new { f.ReportDefinitionID, f.ValueJson })
				.ToListAsync();

			HashSet<int> allowByPublisher = new();
			if (publisherId.HasValue)
			{
				var token = $"\"{publisherId.Value}\"";
				foreach (var x in pubFilt)
				{
					var vj = x.ValueJson ?? "";
					// 粗配對：同時容忍 [1,2] 與 ["1","2"]
					if (vj.Contains(token) || vj.Contains($"[{publisherId.Value}]") || vj.Contains($",{publisherId.Value},"))
						allowByPublisher.Add(x.ReportDefinitionID);
				}
			}

			// 這些定義「有 Publisher 篩選」的 id
			var defsWithPub = pubFilt.Select(x => x.ReportDefinitionID).ToHashSet();

			// 最終：包含兩類
			// 1) 有 Publisher 篩選且包含當前 publisherId
			// 2) 根本沒有 Publisher 篩選（全域可用）
			var result = defs.Where(d =>
					(publisherId.HasValue ? allowByPublisher.Contains(d.ReportDefinitionID) : true)
					|| !defsWithPub.Contains(d.ReportDefinitionID))
				.OrderBy(d => d.ReportName)
				.Select(d => new { id = d.ReportDefinitionID, name = d.ReportName })
				.ToList();

			return Json(result);
		}
	}
}
