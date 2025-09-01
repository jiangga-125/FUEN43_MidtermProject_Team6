namespace ReportMail.Data.Entities
{
    // 查詢結果 DTO，對應 View 輸出
    public class ChartPoint
    {
        public string Label { get; set; } = default!;  // 或可用 DateTime，這邊先用字串泛用
        public decimal Value { get; set; }
    }
}
