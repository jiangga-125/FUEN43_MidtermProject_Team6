using Microsoft.AspNetCore.Mvc;
using ReportMail.Models.Dto;
using ReportMail.Services;
using ReportMail.Services.Export;
using System;
using System.Linq;
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

		public ReportExportController(IExcelExporter excel, MailService mail)
		{
			_excel = excel;
			_mail = mail;
		}

		[HttpPost]
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

			// 依Category、BaseKind、Granularity三者組合，決定兩個欄位名稱
			var (labelHeader, valueHeader) = ResolveHeaders(cat, kind, gran, dto.Title);

			// 呼叫 Exporter
			var series = dto.Labels.Zip(dto.Values, (l, v) => (label: l, value: v)).ToList();
			var (bytes, fileName, contentType) = await _excel.ExportSeriesAsync(
				title: dto.Title ?? "報表",
				subTitle: dto.SubTitle ?? string.Empty,
				series: series,
				sheetName: sheetName,
				labelHeader: labelHeader,
				valueHeader: valueHeader
			);

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

			_mail.SendReport(
				to: dto.To,
				subject: subject,
				body: body,
				attachmentName: safeName,
				attachmentBytes: bytes,
				contentType: string.IsNullOrWhiteSpace(contentType)
					? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
					: contentType
			);

			return Ok(new { message = "已寄出", to = dto.To, file = safeName });
		}

		// ===== helpers =====

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

		// === 欄位標題照表：Category + BaseKind + Granularity → (左欄, 右欄) ===
		private static (string labelHeader, string valueHeader)
			ResolveHeaders(string category, string baseKind, string granularity, string? title)
		{
			string FallbackValue(string? t) => string.IsNullOrWhiteSpace(t) ? "數值" : t;

			if (category == "line")
			{
				// ★ 折線圖左欄依 granularity 顯示「年/⽉/⽇」
				var left = granularity switch
				{
					"year" => "年",
					"month" => "月",
					"day" => "日",
					_ => "日期"
				};

				// 右欄依 BaseKind 給常用語意，未知就退回 Title
				var right = baseKind switch
				{
					"borrow" => "借閱次數",
					"sales" or "orders" => "銷售金額",
					_ => FallbackValue(title)
				};
				return (left, right);
			}

			if (category == "bar")
			{
				var left = baseKind switch
				{
					"sales" => "書名",
					"borrow" => "書名",
					_ => "項目"
				};
				var right = baseKind switch
				{
					"sales" => "本數",
					"borrow" => "借閱次數",
					_ => FallbackValue(title)
				};
				return (left, right);
			}

			if (category == "pie")
			{
				var left = baseKind switch
				{
					"borrow" => "種類",
					"sales" => "書名",
					_ => "項目"
				};
				var right = baseKind switch
				{
					"borrow" => "借閱次數",
					"sales" => "本數",
					_ => FallbackValue(title)
				};
				return (left, right);
			}

			// 其他未知類型
			return ("項目", FallbackValue(title));
		}
	}
}
