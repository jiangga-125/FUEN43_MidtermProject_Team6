using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ReportMail.Models.Dto
{
	public class ReportExportDto
	{
		// ===== 既有欄位 =====
		[Required, EmailAddress]
		public string To { get; set; } = "";

		[Required]              // line | bar | pie
		public string Category { get; set; } = "line";

		[Required]
		public string Title { get; set; } = "報表";

		public string? SubTitle { get; set; }

		// 既有：資料來源
		public string? BaseKind { get; set; }     // sales | borrow | orders

		// 折線圖用的顆粒度；bar/pie 可不填
		public string? Granularity { get; set; }  // day | month | year | none

		[Required]
		public List<string> Labels { get; set; } = new();

		[Required]
		public List<decimal> Values { get; set; } = new();

		// 指標：amount | count | quantity
		public string? ValueMetric { get; set; }

		// 前端送來的圖（可含 data:image/png;base64, 前綴）
		public string? ChartImageBase64 { get; set; }

		// ===== 新增：顯示用（中文化） =====

		/// <summary>
		/// 粉紅標籤整串字（前端已排好，例如：「類型：圓餅圖  來源：借閱紀錄  維度：書籍分類  指標：筆數」）
		/// 後端可直接印；若為 null/空字串，請改用 Display 或伺服器端對照表自行組出。
		/// </summary>
		public string? ChipsText { get; set; }

		/// <summary>
		/// 結構化的中文顯示文字；優先權低於 ChipsText，但可用來自行組粉紅條或其他標示。
		/// </summary>
		public DisplayTextPayload? Display { get; set; }
	}

	/// <summary>
	/// 前端已翻好的中文顯示文字（選擇性）
	/// </summary>
	public class DisplayTextPayload
	{
		public string? ChartTypeText { get; set; }   // 折線圖/長條圖/圓餅圖
		public string? BaseKindText { get; set; }    // 借閱紀錄/訂單/庫存/銷售...
		public string? DimensionText { get; set; }   // 書籍分類/出版社/作者/會員...
		public string? GranularityText { get; set; } // 日/週/月/年/不分粒度
		public string? MetricText { get; set; }      // 筆數/金額/數量/總額...
		public List<string>? Chips { get; set; }     // 若要逐個 chip 顯示可用（例如每個前面加小圓角底色）
	}
}
