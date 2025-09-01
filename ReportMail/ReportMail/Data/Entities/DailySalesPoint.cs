using System;

namespace ReportMail.Data.Entities
{
    // 查詢結果用的 DTO，沒有對應資料表
    public class DailySalesPoint
    {
        public DateTime Day { get; set; }
        public decimal Amount { get; set; }
    }
}
