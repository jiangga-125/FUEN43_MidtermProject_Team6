using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Entities
{
    // 只拿來承接查詢結果，不對應實體表／沒有主鍵
    [Keyless]
    public class ChartPoint
    {
        public string Label { get; set; } = default!;  // 或可用 DateTime，這邊先用字串泛用
        public decimal Value { get; set; }
    }
}
