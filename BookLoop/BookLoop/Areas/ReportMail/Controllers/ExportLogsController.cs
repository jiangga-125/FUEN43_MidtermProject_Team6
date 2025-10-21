using BookLoop.Areas.ReportMail.ViewModels;
using BookLoop.Models;
using BookLoop.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Models.Dto;                 // 方案A：統一使用 Dto 版 ExportSnapshot
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookLoop.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Authorize(Policy = "ReportMail.Logs.Index")]
	public class ExportLogsController : Controller
	{
		private readonly ReportMailDbContext _db;
		private readonly ShopDbContext _shop;
		private readonly IExcelExporter _excel;

		public ExportLogsController(ReportMailDbContext db, ShopDbContext shop, IExcelExporter excel)
		{ _db = db; _shop = shop; _excel = excel; }

		// ====== 列表 ======
		public async Task<IActionResult> Index(
			string? email, int? userId, int? supplierId, int? definitionId,
			string? format, byte? status,
			DateTime? from, DateTime? to,
			int page = 1, int pageSize = 50)
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

			// 批次把名稱對應起來
			var supplierIds = items.Where(x => x.SupplierID != null).Select(x => x.SupplierID!.Value).Distinct().ToArray();
			var defIds = items.Where(x => x.DefinitionID != null).Select(x => x.DefinitionID!.Value).Distinct().ToArray();

			var supplierMap = await _shop.Suppliers
				.Where(s => supplierIds.Contains(s.SupplierID))
				.Select(s => new { s.SupplierID, s.SupplierName })
				.ToDictionaryAsync(x => x.SupplierID, x => x.SupplierName);

			var defMap = await _db.ReportDefinitions
				.Where(d => defIds.Contains(d.ReportDefinitionID))
				.Select(d => new { d.ReportDefinitionID, d.ReportName })
				.ToDictionaryAsync(x => x.ReportDefinitionID, x => x.ReportName);

			// 投影 VM（列表所需欄位）
			var vms = items.Select(r => new ExportLogVM
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
				SupplierName = r.SupplierID != null && supplierMap.TryGetValue(r.SupplierID.Value, out var sname) ? sname : null,
				DefinitionName = r.DefinitionID != null && defMap.TryGetValue(r.DefinitionID.Value, out var dname) ? dname : null
			}).ToList();

			ViewBag.Total = total;
			ViewBag.Page = page;
			ViewBag.PageSize = pageSize;

			return View(vms);
		}

		// ====== 明細 ======
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

			var vm = new ExportLogVM
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
					: JsonSerializer.Serialize(
						JsonSerializer.Deserialize<object>(r.Filters!)!,
						new JsonSerializerOptions { WriteIndented = true })
			};

			return View(vm);
		}

		// ====== 下載（從快照重建，不落地）======
		[HttpGet]
		public async Task<IActionResult> DownloadExcel(long id)
		{
			var log = await _db.ReportExportLogs.AsNoTracking()
						.FirstOrDefaultAsync(x => x.ExportID == id);
			if (log == null) return NotFound();
			if (string.IsNullOrWhiteSpace(log.SnapshotJson))
				return NotFound("此筆紀錄沒有可用快照。");

			var snap = JsonSerializer.Deserialize<ExportSnapshot>(log.SnapshotJson!)!;
			if (snap?.Labels == null || snap.Values == null || snap.Labels.Count != snap.Values.Count)
				return BadRequest("快照資料不完整。");

			var (labelHeader, valueHeader) = ResolveHeaders(
				snap.Category ?? "", snap.BaseKind ?? "", snap.Granularity ?? "", snap.Title, snap.ValueMetric);

			var chartKind = (snap.Category ?? "").Trim().ToLowerInvariant() switch
			{
				"line" => ChartKind.Line,
				"pie" => ChartKind.Pie,
				_ => ChartKind.Column
			};

			var series = snap.Labels.Zip(snap.Values, (l, v) => (label: l, value: v)).ToList();
			var sheetName = MakeSafeSheetName($"{(snap.Category ?? "Report")}Report");

			var (bytes, _, contentType) = await _excel.ExportSeriesAsync(
				title: snap.Title ?? (log.ReportName ?? "報表"),
				subTitle: snap.SubTitle ?? string.Empty,
				series: series,
				sheetName: sheetName,
				chartKind: chartKind,
				labelHeader: labelHeader,
				valueHeader: valueHeader
			);

			var downloadName = string.IsNullOrWhiteSpace(log.AttachmentFileName)
				? MakeSafeFileName(log.ReportName ?? "report") + ".xlsx"
				: log.AttachmentFileName;

			return File(bytes,
				string.IsNullOrWhiteSpace(contentType)
					? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
					: contentType,
				downloadName);
		}

		[HttpGet]
		public async Task<IActionResult> DownloadPdf(long id)
		{
			var log = await _db.ReportExportLogs.AsNoTracking()
						.FirstOrDefaultAsync(x => x.ExportID == id);
			if (log == null) return NotFound();
			if (string.IsNullOrWhiteSpace(log.SnapshotJson))
				return NotFound("此筆紀錄沒有可用快照。");

			var snap = JsonSerializer.Deserialize<ExportSnapshot>(log.SnapshotJson!)!;
			if (snap?.Labels == null || snap.Values == null || snap.Labels.Count != snap.Values.Count)
				return BadRequest("快照資料不完整。");

			var pdfBytes = await BuildPdfBytesAsync(
				title: snap.Title ?? (log.ReportName ?? "報表"),
				subTitle: snap.SubTitle ?? string.Empty,
				labels: snap.Labels, values: snap.Values,
				chartDataUrl: snap.ChartImageBase64
			);

			var downloadName = string.IsNullOrWhiteSpace(log.AttachmentFileName)
				? MakeSafeFileName(log.ReportName ?? "report") + ".pdf"
				: log.AttachmentFileName;

			return File(pdfBytes, "application/pdf", downloadName);
		}

		// ====== Helpers ======

		// B/KB/MB/GB
		private static string BytesHuman(int bytes)
		{
			string[] units = { "B", "KB", "MB", "GB", "TB" };
			double size = bytes; int u = 0;
			while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
			return $"{size:0.##} {units[u]}";
		}

		// PDF（QuestPDF）即時生成
		private static Task<byte[]> BuildPdfBytesAsync(
			string title, string subTitle, IList<string> labels, IList<decimal> values, string? chartDataUrl)
		{
			QuestPDF.Settings.License = LicenseType.Community;

			// 嘗試解析 chart dataURL
			byte[]? chartBytes = null;
			if (!string.IsNullOrWhiteSpace(chartDataUrl))
			{
				try
				{
					var s = chartDataUrl!;
					var comma = s.IndexOf(',');
					if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
						s = s[(comma + 1)..];
					chartBytes = Convert.FromBase64String(s);
				}
				catch { chartBytes = null; }
			}

			return Task.Run(() =>
			{
				using var ms = new MemoryStream();

				var series = labels.Zip(values, (l, v) => (l, v)).ToList();
				var total = series.Sum(x => x.v);
				var max = series.Any() ? series.Max(x => x.v) : 0m;

				Document.Create(c =>
				{
					c.Page(p =>
					{
						p.Margin(36);
						p.Size(PageSizes.A4);
						p.DefaultTextStyle(x => x.FontSize(12));

						p.Header().Column(col =>
						{
							col.Item().Text(title).Bold().FontSize(18);
							if (!string.IsNullOrWhiteSpace(subTitle))
								col.Item().Text(subTitle).FontSize(10).FontColor(Colors.Grey.Darken2);
						});

						const float px = 390f;
						const float ChartHeightPt = px * 72f / 96f;

						p.Content().Column(col =>
						{
							if (chartBytes != null)
							{
								col.Item().AlignCenter().Height(ChartHeightPt)
								   .Image(chartBytes).FitHeight();
								col.Item().PaddingVertical(6);
							}

							col.Item().PaddingBottom(10)
							   .Text($"總和：{total:#,0.##}　最高值：{max:#,0.##}")
							   .FontSize(10).FontColor(Colors.Grey.Darken2);

							col.Item().Table(t =>
							{
								t.ColumnsDefinition(c2 =>
								{
									c2.ConstantColumn(40);
									c2.RelativeColumn();
									c2.ConstantColumn(120);
								});

								t.Header(h =>
								{
									h.Cell().Element(H).Text("#");
									h.Cell().Element(H).Text("Label");
									h.Cell().Element(H).AlignRight().Text("Value");
								});

								for (int i = 0; i < series.Count; i++)
								{
									var row = series[i];
									t.Cell().Element(B).Text((i + 1).ToString());
									t.Cell().Element(B).Text(row.l);
									t.Cell().Element(B).AlignRight().Text(row.v.ToString("#,0.##"));
								}

								static IContainer H(IContainer x) =>
									x.PaddingVertical(6).Background(Colors.Grey.Lighten3)
									 .BorderBottom(1).BorderColor(Colors.Grey.Medium);

								static IContainer B(IContainer x) =>
									x.PaddingVertical(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
							});
						});

						p.Footer().DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Darken1))
						   .AlignRight()
						   .Text(t => { t.Span("頁碼 "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
					});
				})
				.GeneratePdf(ms);

				return ms.ToArray();
			});
		}

		// 欄位標題推導（與 ReportExportController 對齊）
		private static (string labelHeader, string valueHeader)
			ResolveHeaders(string category, string baseKind, string granularity, string? title, string? valueMetric = null)
		{
			static string N(string? s) => (s ?? "").Trim().ToLowerInvariant();

			static string NormalizeBase(string? baseKind)
			{
				var k = N(baseKind);
				return k switch { "order" => "orders", _ => k };
			}

			static string FallbackValue(string? t) => string.IsNullOrWhiteSpace(t) ? "數值" : t;

			static string MetricRightLabel(string baseKind, string? metric, string defaultLabel)
			{
				var m = N(metric);
				var b = NormalizeBase(baseKind);

				return (m, b) switch
				{
					("count", "orders") => "訂單筆數",
					("amount", "orders") => "銷售金額",
					("total", "orders") => "銷售金額",
					("count", "sales") => "銷售本數",
					("quantity", "sales") => "銷售本數",
					("amount", "sales") => "銷售金額",
					("total", "sales") => "銷售金額",
					("count", "borrow") => "借閱次數",
					("quantity", "borrow") => "借閱次數",
					_ => defaultLabel
				};
			}

			static string GranLeft(string g) => N(g) switch
			{
				"year" => "年份",
				"month" => "月份",
				"week" => "週",
				"day" => "日期",
				_ => "時間"
			};

			var cat = N(category);
			var baseK = NormalizeBase(baseKind);

			if (cat == "line")
			{
				var left = GranLeft(granularity);
				var rightDefault = baseK switch
				{
					"borrow" => "借閱次數",
					"sales" => "銷售金額",
					"orders" => "銷售金額",
					_ => FallbackValue(title)
				};
				var right = MetricRightLabel(baseK, valueMetric, rightDefault);
				return (left, right);
			}

			if (cat == "bar")
			{
				var left = baseK switch
				{
					"sales" => "書名",
					"borrow" => "書名",
					_ => "項目"
				};
				var rightDefault = baseK switch
				{
					"sales" => "銷售本數",
					"borrow" => "借閱本數",
					"orders" => "銷售金額",
					_ => FallbackValue(title)
				};
				var right = MetricRightLabel(baseK, valueMetric, rightDefault);
				return (left, right);
			}

			if (cat == "pie")
			{
				var left = baseK switch
				{
					"borrow" => "種類",
					"sales" => "書名",
					"orders" => "項目",
					_ => "項目"
				};
				var rightDefault = baseK switch
				{
					"borrow" => "借閱次數",
					"sales" => "銷售本數",
					"orders" => "銷售金額",
					_ => FallbackValue(title)
				};
				var right = MetricRightLabel(baseK, valueMetric, rightDefault);
				return (left, right);
			}

			return ("項目", FallbackValue(title));
		}

		private static string MakeSafeFileName(string name)
		{
			var invalid = System.IO.Path.GetInvalidFileNameChars();
			foreach (var c in invalid) name = name.Replace(c, '_');
			name = name.Trim();
			return string.IsNullOrWhiteSpace(name) ? "report" : name;
		}

		private static string MakeSafeSheetName(string name)
		{
			var safe = System.Text.RegularExpressions.Regex.Replace(name, @"[:\\/\?\*\[\]]", "_");
			if (safe.Length > 31) safe = safe[..31];
			return string.IsNullOrWhiteSpace(safe) ? "Report" : safe.Trim();
		}

		// （只有從主頁即時匯出才會用到；在 Logs Controller 內保留給可能的擴充）
		private static ExportSnapshot MakeSnapshot(ReportExportDto dto) => new()
		{
			Category = dto.Category,
			BaseKind = dto.BaseKind,
			Granularity = dto.Granularity,
			ValueMetric = dto.ValueMetric,
			Title = dto.Title,
			SubTitle = dto.SubTitle,
			DefinitionId = dto.DefinitionId,
			Labels = dto.Labels?.ToList(),
			Values = dto.Values?.Select(v => Convert.ToDecimal(v)).ToList(),
			ChartImageBase64 = dto.ChartImageBase64
		};

		private static string ToJson(object o) =>
			JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false });
	}
}
