using System;

namespace BookLoop.Areas.ReportMail.ViewModels
{
	public class ExportLogVM
	{
		public long ExportID { get; set; }
		public DateTime RequestedAt { get; set; }
		public string Format { get; set; } = "";
		public byte Status { get; set; }
		public string TargetEmail { get; set; } = "";
		public string AttachmentFileName { get; set; } = "";
		public int AttachmentBytes { get; set; }
		public string SizeText { get; set; } = "";           // ★ 人類可讀大小

		public string ReportName { get; set; } = "";
		public string Category { get; set; } = "";

		public int UserID { get; set; }
		public int? SupplierID { get; set; }
		public int? DefinitionID { get; set; }

		public string? SupplierName { get; set; }            // ★ 書商名稱
		public string? DefinitionName { get; set; }          // ★ 定義名稱

		public string? PolicyUsed { get; set; }
		public string? Ip { get; set; }
		public string? UserAgent { get; set; }
		public string? FiltersPretty { get; set; }           // 詳情頁用（已格式化）
		public byte[]? AttachmentChecksum { get; set; }      // 詳情頁顯示完整 checksum
	}
}

