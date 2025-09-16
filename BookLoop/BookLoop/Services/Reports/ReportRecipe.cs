// Services/Reports/ReportRecipe.cs
namespace BookLoop.Services.Reports
{
    public class ReportRecipe
    {
        public ReportSource Source { get; set; } = new();
        public ReportDimension Dimension { get; set; } = new();
        public ReportMetric Metric { get; set; } = new();
        public string? WherePreset { get; set; }
    }

    public class ReportSource
    {
        public string Type { get; set; } = "";   // "table" 或 "query"
        public string Name { get; set; } = "";   // 表名或子查詢
        public string Alias { get; set; } = "";   // 別名（例如 "o", "br"）
    }

    public class ReportDimension
    {
        public string Column { get; set; } = "";   // 維度欄位（可含別名）
        public string Type { get; set; } = "";   // "date"/"string"/...
        public string Granularity { get; set; } = "day";
        public string? LabelFormat { get; set; }        // 可選的標籤格式
    }

    public class ReportMetric
    {
        public string Expr { get; set; } = "";         // 指標運算式（不要含 AS）
        public string Alias { get; set; } = "Value";    // 指標別名（程式會用在 SELECT ... AS {Alias}）
    }
}
