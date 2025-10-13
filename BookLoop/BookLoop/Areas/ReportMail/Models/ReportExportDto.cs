using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ReportMail.Models.Dto
{
	public class ReportExportDto
	{
		[Required, EmailAddress]
		public string To { get; set; } = "";

		[Required]              // line | bar | pie
		public string Category { get; set; } = "line";

		[Required]
		public string Title { get; set; } = "報表";

		public string? SubTitle { get; set; }

		// 既有：資料來源
		public string? BaseKind { get; set; }     // sales | borrow | orders

		//折線圖用的顆粒度；bar/pie 可不填
		public string? Granularity { get; set; }  // day | month | year

		[Required] 
		public List<string> Labels { get; set; } = new();
		[Required] 
		public List<decimal> Values { get; set; } = new();
		public string? ValueMetric { get; set; }// amount | count | quantity
        public string? ChartImageBase64 { get; set; }  // data:image/png;base64

    }
}
