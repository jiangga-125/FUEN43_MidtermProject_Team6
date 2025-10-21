using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;
using BookLoop.Areas.ReportMail.ViewModels;

namespace BookLoop.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Authorize(Policy = "ReportMail.Logs.Index")]
	public class ExportLogsController : Controller
	{
		private readonly ReportMailDbContext _db;
		private readonly ShopDbContext _shop;
		public ExportLogsController(ReportMailDbContext db, ShopDbContext shop)
		{ _db = db; _shop = shop; }

		public async Task<IActionResult> Index(
	string? email, int? userId, int? supplierId, int? definitionId,
	string? format, byte? status,
	DateTime? from, DateTime? to, int page = 1, int pageSize = 50)
		{
			var q = _db.ReportExportLogs.AsNoTracking()
				.OrderByDescending(x => x.RequestedAt)
				.AsQueryable();

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

			// 批次把名稱對應起來（只撈存在的欄位）
			var supplierIds = items.Where(x => x.SupplierID != null).Select(x => x.SupplierID!.Value).Distinct().ToArray();
			var defIds = items.Where(x => x.DefinitionID != null).Select(x => x.DefinitionID!.Value).Distinct().ToArray();

			var supplierMap = await _shop.Suppliers
				.Where(s => supplierIds.Contains(s.SupplierID))
				.Select(s => new { s.SupplierID, s.SupplierName })                 // ← 不碰 SupplierCode
				.ToDictionaryAsync(x => x.SupplierID, x => x.SupplierName);

			var defMap = await _db.ReportDefinitions
				.Where(d => defIds.Contains(d.ReportDefinitionID))
				.Select(d => new { d.ReportDefinitionID, d.ReportName })
				.ToDictionaryAsync(x => x.ReportDefinitionID, x => x.ReportName);

			// 投影成 VM（列表只需要的欄位）
			var vms = items.Select(r => new Areas.ReportMail.ViewModels.ExportLogVM
			{
				ExportID = r.ExportID,
				RequestedAt = r.RequestedAt,
				Format = r.Format,
				Status = r.Status,
				TargetEmail = r.TargetEmail,
				AttachmentFileName = r.AttachmentFileName,
				AttachmentBytes = r.AttachmentBytes,
				SizeText = BytesHuman(r.AttachmentBytes),                           // ★
				ReportName = r.ReportName ?? "",
				Category = r.Category,
				UserID = r.UserID,
				SupplierID = r.SupplierID,
				DefinitionID = r.DefinitionID,
				SupplierName = r.SupplierID != null && supplierMap.TryGetValue(r.SupplierID.Value, out var sname) ? sname : null,
				DefinitionName = r.DefinitionID != null && defMap.TryGetValue(r.DefinitionID.Value, out var dname) ? dname : null
			}).ToList();

			ViewBag.Total = total;
			ViewBag.Page = page;
			ViewBag.PageSize = pageSize;

			return View(vms); // ★ 回傳 VM
		}

		public async Task<IActionResult> Details(long id)
		{
			var r = await _db.ReportExportLogs.AsNoTracking().FirstOrDefaultAsync(x => x.ExportID == id);
			if (r == null) return NotFound();

			string? supplierName = null, defName = null;
			if (r.SupplierID != null)
				supplierName = await _shop.Suppliers
					.Where(s => s.SupplierID == r.SupplierID)
					.Select(s => s.SupplierName)
					.FirstOrDefaultAsync();

			if (r.DefinitionID != null)
				defName = await _db.ReportDefinitions
					.Where(d => d.ReportDefinitionID == r.DefinitionID)
					.Select(d => d.ReportName)
					.FirstOrDefaultAsync();

			var vm = new Areas.ReportMail.ViewModels.ExportLogVM
			{
				ExportID = r.ExportID,
				RequestedAt = r.RequestedAt,
				Format = r.Format,
				Status = r.Status,
				TargetEmail = r.TargetEmail,
				AttachmentFileName = r.AttachmentFileName,
				AttachmentBytes = r.AttachmentBytes,
				SizeText = BytesHuman(r.AttachmentBytes),
				ReportName = r.ReportName ?? "",
				Category = r.Category,
				UserID = r.UserID,
				SupplierID = r.SupplierID,
				DefinitionID = r.DefinitionID,
				SupplierName = supplierName,
				DefinitionName = defName,
				PolicyUsed = r.PolicyUsed,
				Ip = r.Ip,
				UserAgent = r.UserAgent,
				AttachmentChecksum = r.AttachmentChecksum,
				FiltersPretty = string.IsNullOrWhiteSpace(r.Filters)
					? "(empty)"
					: System.Text.Json.JsonSerializer.Serialize(
						  System.Text.Json.JsonSerializer.Deserialize<object>(r.Filters!)!,
						  new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
			};

			return View(vm); // ★ 回傳 VM
		}


		// 把位元組轉成 B/KB/MB/GB
		private static string BytesHuman(int bytes)
		{
			string[] units = { "B", "KB", "MB", "GB", "TB" };
			double size = bytes; int u = 0;
			while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
			return $"{size:0.##} {units[u]}";
		}

	}
}
