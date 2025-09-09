// Services/Reports/IReportDataService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReportMail.Services.Reports
{
    /// <summary>
    /// 報表資料讀取的統一介面。
    /// 之後若你要切換到原生 SQL、Dapper、或其他資料來源，只要換掉實作即可。
    /// </summary>
    public interface IReportDataService
    {
        /// <summary>
        /// 折線圖：總銷售金額（依顆粒度彙總）
        /// granularity = day/month/year
        /// excludeStatuses 預設排除「已取消」(4)
        /// </summary>
        Task<IReadOnlyList<ChartPoint>> GetSalesAmountSeriesAsync(
            DateTime start, DateTime end, string granularity = "day",
            int[]? excludeStatuses = null);

        /// <summary>
        /// 長條圖：近一段期間「銷售書籍排行」（以數量排序）
        /// </summary>
        Task<IReadOnlyList<ChartPoint>> GetTopSoldBooksAsync(
            DateTime start, DateTime endInclusive, int topN = 10, int[]? excludeStatuses = null);

        /// <summary>
        /// 圓餅圖：近一段期間「借閱書籍種類」排行（以借閱筆數計）
        /// </summary>
        Task<IReadOnlyList<ChartPoint>> GetTopBorrowCategoryAsync(
            DateTime start, DateTime endInclusive, int topN = 5);
    }
}
