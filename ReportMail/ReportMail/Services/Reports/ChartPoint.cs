// Services/Reports/ChartPoint.cs
namespace ReportMail.Services.Reports
{
    /// <summary>圖表的基本資料點：X 軸標籤 + 數值</summary>
    public class ChartPoint
    {
        public string Label { get; set; } = default!;
        public decimal Value { get; set; }
    }
}
