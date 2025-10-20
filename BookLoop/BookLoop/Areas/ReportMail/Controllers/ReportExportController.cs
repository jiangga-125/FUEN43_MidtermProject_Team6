using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services;
using BookLoop.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportMail.Models.Dto;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



namespace ReportMail.Areas.ReportMail.Controllers
{
	[Area("ReportMail")]
	[Route("ReportMail/[controller]/[action]")]
	public class ReportExportController : Controller
	{
		private readonly IExcelExporter _excel;
		private readonly MailService _mail;
        private readonly ReportMailDbContext _db;
        private readonly ShopDbContext _shop;


        public ReportExportController(IExcelExporter excel, MailService mail, ReportMailDbContext db, ShopDbContext shop)
		{
			_excel = excel;
			_mail = mail;
            _db = db;
            _shop = shop;
        }

		[HttpPost]
        [Authorize(Policy = "ReportMail.Export.Excel")]
        public async Task<IActionResult> SendExcel([FromBody] ReportExportDto dto)
		{
			// ---- 0) 基本驗證 ----
			if (dto is null)
				return BadRequest("缺少必要參數");

			if (string.IsNullOrWhiteSpace(dto.To))
				return BadRequest("請輸入收件者 Email");

			if (dto.Labels is null || dto.Values is null)
				return BadRequest("沒有可匯出的資料");

			if (dto.Labels.Count != dto.Values.Count)
				return BadRequest($"資料長度不一致：labels={dto.Labels.Count}, values={dto.Values.Count}");

			// ---- 1) 產生 Excel ----
			// sheetName：用 Category + "Report"，再安全化（Excel 工作表名稱禁止特殊字）
			var sheetName = MakeSafeSheetName($"{(dto.Category ?? "Report")}".Trim() + "Report");

			var cat = (dto.Category ?? "").Trim().ToLowerInvariant();
			var kind = (dto.BaseKind ?? "").Trim().ToLowerInvariant();
			var gran = (dto.Granularity ?? "").Trim().ToLowerInvariant();

			// 依Category、BaseKind、Granularity三者組合加上ValueMetric，決定兩個欄位名稱
			var (labelHeader, valueHeader) = ResolveHeaders(cat, kind, gran, dto.Title, dto.ValueMetric);


			// Category → ChartKind
			var chartKind = (dto.Category ?? "").Trim().ToLowerInvariant() switch
			{
				"line" => ChartKind.Line,
				"pie" => ChartKind.Pie,
				_ => ChartKind.Column
			};

			//  組 series 並呼叫帶圖型重載
			var series = dto.Labels.Zip(dto.Values, (l, v) => (label: l, value: v)).ToList();
			var (bytes, fileName, contentType) = await _excel.ExportSeriesAsync(
				title: dto.Title ?? "報表",
				subTitle: dto.SubTitle ?? string.Empty,
				series: series,
				sheetName: sheetName,
				chartKind: chartKind,
				labelHeader: labelHeader,
				valueHeader: valueHeader
			);

            const int MAX_BYTES = 15 * 1024 * 1024; // 15MB
            if (bytes == null || bytes.Length == 0)
                return BadRequest("無可匯出資料。");
            if (bytes.Length > MAX_BYTES)
                return BadRequest("匯出檔案過大，請縮小範圍或改為 PDF。");

            // ---- 2) 組附件檔名（用 Title）----
            // 要求：附件名稱 = 下拉選到的自訂報表名稱（= 前端送來的 Title）
            // 需過濾非法字元，並確保加上 .xlsx 副檔名
            var safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(dto.Title) ? "report" : dto.Title);
			if (!safeName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
				safeName += ".xlsx";

			// ---- 3) 發信 ----
			var subject = dto.Title ?? "報表匯出";
			var body = $@"
                <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px"">
                    <h3 style=""margin:0 0 8px 0;"">{HtmlEncode(dto.Title ?? "報表匯出")}</h3>
                    {(string.IsNullOrWhiteSpace(dto.SubTitle) ? "" : $"<div style=\"color:#555\">{HtmlEncode(dto.SubTitle!)}</div>")}
                    <p style=""margin-top:12px"">請查收附件。</p>
                </div>";

			await _mail.SendReportAsync(
								to: dto.To,
								subject: subject,
								body: body,
								attachmentName: safeName,
								attachmentBytes: bytes,
								contentType: string.IsNullOrWhiteSpace(contentType)
										? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
										: contentType,
								cancellationToken: HttpContext.RequestAborted
						);

            // 1) 取使用者與書商（_shop 已在你的專案中注入）
            var uid = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var u) ? u : 0;
            int? supplierId = null;
            var s = User.FindFirst("supplier")?.Value;
            if (int.TryParse(s, out var sid)) supplierId = sid;
            if (supplierId is null)
                supplierId = await _shop.SupplierUsers
                    .Where(x => x.UserID == uid)
                    .Select(x => (int?)x.SupplierID)
                    .FirstOrDefaultAsync();

            // 2) 封存這次條件（用你目前 DTO 真的有的欄位）
            // （沒有 DefinitionId/DateRange 沒關係，先不放）
            var filtersJson = JsonSerializer.Serialize(new
            {
                dto.Category,
                dto.BaseKind,
                dto.Granularity,
                dto.ValueMetric,
                dto.Title,
                dto.SubTitle
            });

            // 3) 傳真（檔案雜湊）
            byte[] checksum;
            using (var sha = SHA256.Create())
                checksum = sha.ComputeHash(bytes); // PDF 端點換 pdfBytes

            // 4) B 版欄位完整落庫
            _db.ReportExportLogs.Add(new ReportExportLog
            {
                UserID = uid,
                SupplierID = supplierId,
                DefinitionID = dto.DefinitionId,
                Category = dto.Category ?? "unknown",
                ReportName = dto.Title ?? dto.Category ?? "報表",
                Format = "xlsx",            // PDF 端點改 "pdf"
                TargetEmail = dto.To!,
                AttachmentFileName = safeName,          // 你剛寄的檔名
                AttachmentBytes = bytes.Length,      // PDF 端點換 pdfBytes.Length
                AttachmentChecksum = checksum,
                Status = 1,                 // 1=Sent
                RequestedAt = DateTime.UtcNow,   // DbContext.Partial 不會自動幫這兩個欄位帶值
                ProcessedAt = DateTime.UtcNow,
                Filters = filtersJson,
                TraceId = Guid.NewGuid(),
                PolicyUsed = "ReportMail.Export.Excel",   // PDF 端點改 "ReportMail.Export.Pdf"
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok(new { ok = true, message = "已寄出", to = dto.To, file = safeName });
		}

		[HttpPost]
        [Authorize(Policy = "ReportMail.Export.Pdf")]
        public async Task<IActionResult> SendPdf([FromBody] ReportExportDto dto)
		{
			if (dto is null) return BadRequest("缺少必要參數");
			if (string.IsNullOrWhiteSpace(dto.To)) return BadRequest("請輸入收件者 Email");
			if (dto.Labels is null || dto.Values is null) return BadRequest("沒有可匯出的資料");
			if (dto.Labels.Count != dto.Values.Count) return BadRequest("資料長度不一致");

			var cat = (dto.Category ?? "").Trim().ToLowerInvariant();
			var kind = (dto.BaseKind ?? "").Trim().ToLowerInvariant();
			var gran = (dto.Granularity ?? "").Trim().ToLowerInvariant();
			var (labelHeader, valueHeader) = ResolveHeaders(cat, kind, gran, dto.Title, dto.ValueMetric);

			var series = dto.Labels.Zip(dto.Values, (l, v) => (label: l ?? string.Empty, value: v)).ToList();

			// ★ 解析前端傳來的圖表 base64（允許帶 data:image/png;base64, 前綴）
			byte[]? chartBytes = null;
			if (!string.IsNullOrWhiteSpace(dto.ChartImageBase64))
			{
				var s = dto.ChartImageBase64!;
				var comma = s.IndexOf(',');
				if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
					s = s.Substring(comma + 1);
				try { chartBytes = Convert.FromBase64String(s); } catch { chartBytes = null; }
			}

			QuestPDF.Settings.License = LicenseType.Community;

			byte[] pdfBytes;
			using (var ms = new MemoryStream())
			{
				// 1) 先用你原本的規則推導標頭（唯一權威）
				var headers = ResolveHeaders(
					dto.Category,           // line | bar | pie
					dto.BaseKind ?? "",     // orders | sales | borrow ...
					dto.Granularity ?? "",  // year | month | week | day | ...
					dto.Title,              // 現有標題
					dto.ValueMetric         // count | amount | quantity | ...
				);
				string labelHeaderText = headers.labelHeader;
				string valueHeaderText = headers.valueHeader;

				// 2) 類型（僅作最小對應，無引入新規則）
				string typeText = dto.Category switch
				{
					"line" => "折線圖",
					"bar" => "長條圖",
					"pie" => "圓餅圖",
					_ => dto.Category ?? "圖表"
				};

				// 3) 指標：完全以 ResolveHeaders 的「右欄標題」為準
				string metricText = valueHeader;

				// 4) 是否有粒度：必須同時符合「前端有帶 granularity」且「左欄為時間類」才顯示
				bool hasGranularity =
					!string.IsNullOrWhiteSpace(dto.Granularity) &&
					(labelHeaderText is "年份" or "月份" or "週" or "日期" or "時間");

				// 5) 來源：不新增規則，從 ResolveHeaders 推出的「右欄標題」反推常見來源詞；若判不出，就原樣使用 baseKind
				string sourceText =
					valueHeader.Contains("借閱", StringComparison.Ordinal) ? "借閱" :
					valueHeader.Contains("銷售", StringComparison.Ordinal) ? "銷售" :
					valueHeader.Contains("訂單", StringComparison.Ordinal) ? "訂單" :
					(dto.BaseKind ?? "來源");

				// 6) 組 chips（沒有粒度就不加入那個項目）
				var chips = new List<string>
					{
						$"類型：{typeText}",
						$"來源：{sourceText}",
						$"指標：{metricText}"
					};
				if (hasGranularity)
					chips.Add($"粒度：{labelHeaderText}");

				string chipsLine = string.Join("  ", chips);

				var doc = Document.Create(container =>
				{
					container.Page(page =>
					{
						page.Margin(36);
						page.Size(PageSizes.A4);
						page.DefaultTextStyle(x => x.FontSize(12));

						page.Header().Column(col =>
						{
							col.Item().Text(dto.Title ?? "報表匯出").Bold().FontSize(18);
							if (!string.IsNullOrWhiteSpace(dto.SubTitle))
								col.Item().Text(dto.SubTitle!).FontSize(10).FontColor(Colors.Grey.Darken2);
							col.Item().Text(chipsLine).FontSize(9).FontColor(Colors.Grey.Darken1);
						});

						// 以瀏覽器常見的 96dpi 對應：pt = px * 72 / 96
						const float px = 390f;
						const float ChartHeightPt = px * 72f / 96f;  

						page.Content()
						.Background(Colors.White)       // 白色底，避免透明背景變灰
						.Column(col =>
						{
								if (chartBytes != null)
								{
                                // 固定高度 ≈ 390px（約 292.5pt），等比縮放以「高度」為準
                                col.Item()
									   .AlignCenter()           // 水平置中整個內容
									   .Height(ChartHeightPt)   // 固定容器高度（約 140px）
									   .Image(chartBytes)
									   .FitHeight();            // 依高度等比縮放，不超框

									col.Item().PaddingVertical(6); // 圖與圖之間留白
								}

						// 統計摘要
						var total = series.Sum(s => s.value);
						var max = series.Any() ? series.Max(s => s.value) : 0m;
						col.Item().PaddingBottom(10)
								  .Text($"總和：{total:#,0.##}　最高值：{max:#,0.##}")
								  .FontSize(10).FontColor(Colors.Grey.Darken2);

						// 明細表
						col.Item().Table(t =>
						{
							t.ColumnsDefinition(c =>
							{
								c.ConstantColumn(40);   // #
								c.RelativeColumn();     // 標籤
								c.ConstantColumn(120);  // 數值
							});

							t.Header(h =>
							{
								h.Cell().Element(H).Text("#");
								h.Cell().Element(H).Text(labelHeader);
								h.Cell().Element(H).AlignRight().Text(valueHeader);
							});

							for (int i = 0; i < series.Count; i++)
							{
								var row = series[i];
								t.Cell().Element(B).Text((i + 1).ToString());
								t.Cell().Element(B).Text(row.label);
								t.Cell().Element(B).AlignRight().Text(row.value.ToString("#,0.##"));
							}

							static IContainer H(IContainer x) =>
								x.PaddingVertical(6).Background(Colors.Grey.Lighten3)
								 .BorderBottom(1).BorderColor(Colors.Grey.Medium);

							static IContainer B(IContainer x) =>
								x.PaddingVertical(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
						});
					});

						page.Footer()
							.DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Darken1))
							.AlignRight()
							.Text(t => { t.Span("頁碼 "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
					});
				});

				doc.GeneratePdf(ms);
				pdfBytes = ms.ToArray();
			}

            const int MAX_BYTES = 15 * 1024 * 1024;
            if (pdfBytes == null || pdfBytes.Length == 0)
                return BadRequest("無可匯出資料。");
            if (pdfBytes.Length > MAX_BYTES)
                return BadRequest("匯出檔案過大，請縮小範圍。");

            var safeName = MakeSafeFileName(dto.Title ?? "report");
			if (!safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) safeName += ".pdf";

			var subject = dto.Title ?? "報表匯出 (PDF)";
			var sb = new StringBuilder();
			sb.AppendLine("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px\">");
			sb.Append("<h3 style=\"margin:0 0 8px 0;\">")
			  .Append(HtmlEncode(dto.Title ?? "報表匯出"))
			  .AppendLine("</h3>");
			if (!string.IsNullOrWhiteSpace(dto.SubTitle))
			{
				sb.Append("<div style=\"color:#555\">")
				  .Append(HtmlEncode(dto.SubTitle!))
				  .AppendLine("</div>");
			}
			sb.AppendLine("<p style=\"margin-top:12px\">請查收附件（PDF）。</p>");
			sb.AppendLine("</div>");
			var body = sb.ToString();

			await _mail.SendReportAsync(
				to: dto.To,
				subject: subject,
				body: body,
				attachmentName: safeName,
				attachmentBytes: pdfBytes,
				contentType: "application/pdf",
				cancellationToken: HttpContext.RequestAborted
			);

            // === PDF 落庫（B 版欄位） ===
            var uid = GetUserId();
            var supplierId = await GetSupplierIdAsync(uid);


            // 只封存你 DTO 目前真的有的查詢上下文
            var filtersJson = JsonSerializer.Serialize(new
            {
                dto.Category,
                dto.BaseKind,
                dto.Granularity,
                dto.ValueMetric,
                dto.Title,
                dto.SubTitle
            });

            byte[] checksumPdf;
            using (var sha = SHA256.Create())
                checksumPdf = sha.ComputeHash(pdfBytes);

            _db.ReportExportLogs.Add(new ReportExportLog
            {
                UserID = uid,
                SupplierID = supplierId,
                DefinitionID = dto.DefinitionId,     // 你 DTO 已有這個屬性
                Category = dto.Category ?? "unknown",
                ReportName = dto.Title ?? dto.Category ?? "報表",
                Format = "pdf",
                TargetEmail = dto.To!,
                AttachmentFileName = safeName,
                AttachmentBytes = pdfBytes.Length,
                AttachmentChecksum = checksumPdf,
                Status = 1,
                RequestedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                Filters = filtersJson,
                TraceId = Guid.NewGuid(),
                PolicyUsed = "ReportMail.Export.Pdf",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });
            await _db.SaveChangesAsync();

            return Ok(new { ok = true, message = "已寄出", to = dto.To, file = safeName });

        }

        // ===== helpers =====

        private int GetUserId()
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private async Task<int?> GetSupplierIdAsync(int userId)
        {
            // 先從 claim 取
            var s = User.FindFirst("supplier")?.Value;
            if (int.TryParse(s, out var sid)) return sid;

            // 沒有則 DB fallback → 這裡要用 _shop.SupplierUsers（不是 _db）
            return await _shop.SupplierUsers
                .Where(x => x.UserID == userId)
                .Select(x => (int?)x.SupplierID)
                .FirstOrDefaultAsync();
        }



        private static string MakeSafeFileName(string name)
		{
			// Windows/檔名非法字元過濾
			var safe = Regex.Replace(name, @"[\\/:*?""<>|]", "_");
			// 避免空白檔名
			return string.IsNullOrWhiteSpace(safe) ? "report" : safe.Trim();
		}

		private static string MakeSafeSheetName(string name)
		{
			// Excel 工作表名稱不可含下列字元：: \ / ? * [ ]
			var safe = Regex.Replace(name, @"[:\\/\?\*\[\]]", "_");
			// 最長 31 字元
			if (safe.Length > 31) safe = safe[..31];
			// 避免空白
			return string.IsNullOrWhiteSpace(safe) ? "Report" : safe.Trim();
		}

		private static string HtmlEncode(string s)
			=> System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

		// === 欄位標題照表：Category + BaseKind + Granularity (+ ValueMetric) → (左欄, 右欄)
		private static (string labelHeader, string valueHeader)
			ResolveHeaders(string category, string baseKind, string granularity, string? title, string? valueMetric = null)
		{
			// 正規化 & 小工具
			static string N(string? s)
				=> (s ?? "").Trim().ToLowerInvariant();

			// 有些地方會用 order / orders，這裡統一
			static string NormalizeBase(string? baseKind)
			{
				var k = N(baseKind);
				return k switch { "order" => "orders", _ => k };
			}

			static string FallbackValue(string? t) => string.IsNullOrWhiteSpace(t) ? "數值" : t;

			// 依 valueMetric 與 baseKind 推右欄名（優先用 metric）
			static string MetricRightLabel(string baseKind, string? metric, string defaultLabel)
			{
				var m = N(metric);
				var b = NormalizeBase(baseKind);

				// 以度量為準：count/quantity/amount/total…
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

					// 其他未知度量 → 用預設
					_ => defaultLabel
				};
			}

			// granularity → 左欄標題（折線圖）
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

				// 先算預設右欄，再看 valueMetric 是否要覆蓋
				var rightDefault = baseK switch
				{
					"borrow" => "借閱次數",
					"sales" => "銷售金額",
					"orders" => "銷售金額", // 未指定度量時視為金額
					_ => FallbackValue(title)
				};
				var right = MetricRightLabel(baseK, valueMetric, rightDefault);
				return (left, right);
			}

			if (cat == "bar")
			{
				// 類別軸多半是書名或項目
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

			// 其他未知類型
			return ("項目", FallbackValue(title));
		}


	}
}
