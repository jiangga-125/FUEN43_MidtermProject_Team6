using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models
{
    public class ReportExportLog
    {
        public long ExportID { get; set; }

        public int UserID { get; set; }
        public int? SupplierID { get; set; }
        public int? DefinitionID { get; set; }

        public string Category { get; set; } = null!;
        public string? ReportName { get; set; }

        public string Format { get; set; } = null!;           // xlsx/pdf/csv
        public string TargetEmail { get; set; } = null!;
        public string AttachmentFileName { get; set; } = null!;
        public int AttachmentBytes { get; set; }
        public byte[]? AttachmentChecksum { get; set; }

        public byte Status { get; set; } = 1;                 // 1=Sent, 2=Failed
        public string? ErrorMessage { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        public string? Filters { get; set; }
        public Guid TraceId { get; set; } = Guid.NewGuid();

        public string? PolicyUsed { get; set; }
        public string? Ip { get; set; }
        public string? UserAgent { get; set; }

		public string? SnapshotJson { get; set; }

		// （選做）若你建立了 FK 再加導覽屬性
		// public ReportDefinition? Definition { get; set; }
		// public Supplier? Supplier { get; set; }
	}
}