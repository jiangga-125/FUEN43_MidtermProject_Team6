using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace BookLoop.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Authorize(Policy = "ReportMail.Logs.Index")]     // 只要能進報表就能看紀錄；你也可改成 ADMIN
	public class ExportLogsController : Controller
	{
		private readonly ReportMailDbContext _db;
		public ExportLogsController(ReportMailDbContext db) => _db = db;

		// /ReportMail/ExportLogs?email=...&userId=...&from=2025-10-01&to=2025-10-22&format=xlsx&status=1
		public async Task<IActionResult> Index(
			string? email, int? userId, int? supplierId, int? definitionId,
			string? format, byte? status,
			DateTime? from, DateTime? to, int page = 1, int pageSize = 50)
		{
			var q = _db.ReportExportLogs.AsNoTracking().OrderByDescending(x => x.RequestedAt).AsQueryable();

			if (!string.IsNullOrWhiteSpace(email)) q = q.Where(x => x.TargetEmail.Contains(email));
			if (userId != null) q = q.Where(x => x.UserID == userId);
			if (supplierId != null) q = q.Where(x => x.SupplierID == supplierId);
			if (definitionId != null) q = q.Where(x => x.DefinitionID == definitionId);
			if (!string.IsNullOrWhiteSpace(format)) q = q.Where(x => x.Format == format);
			if (status != null) q = q.Where(x => x.Status == status);
			if (from != null) q = q.Where(x => x.RequestedAt >= from);
			if (to != null) q = q.Where(x => x.RequestedAt < to);

			var total = await q.CountAsync();
			var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

			ViewBag.Total = total;
			ViewBag.Page = page;
			ViewBag.PageSize = pageSize;
			ViewBag.Email = email;
			ViewBag.UserId = userId;
			ViewBag.SupplierId = supplierId;
			ViewBag.DefinitionId = definitionId;
			ViewBag.Format = format;
			ViewBag.Status = status;
			ViewBag.From = from?.ToString("yyyy-MM-dd");
			ViewBag.To = to?.ToString("yyyy-MM-dd");

			return View(items);
		}

		// 詳細：把 Filters JSON 美化顯示
		public async Task<IActionResult> Details(long id)
		{
			var log = await _db.ReportExportLogs.AsNoTracking().FirstOrDefaultAsync(x => x.ExportID == id);
			if (log == null) return NotFound();
			ViewBag.FiltersPretty = string.IsNullOrWhiteSpace(log.Filters)
				? "(empty)"
				: JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(log.Filters!)!, new JsonSerializerOptions { WriteIndented = true });
			return View(log);
		}
	}
}
