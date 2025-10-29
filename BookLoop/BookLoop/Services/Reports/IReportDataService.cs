// Services/Reports/IReportDataService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookLoop.Services.Reports
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
		/// excludeStatuses 預設排除「已取消」(0)（依你現有實作）
		/// publisherIds = null 代表不限制；傳陣列則只統計該出版社(們)的書籍
		/// </summary>
		Task<IReadOnlyList<ChartPoint>> GetSalesAmountSeriesAsync(
			DateTime start,
			DateTime end,
			string granularity = "day",
			int[]? excludeStatuses = null,
			int[]? publisherIds = null); // ★ 新增：可選出版社過濾

		/// <summary>
		/// 長條圖：近一段期間「銷售書籍排行」（以銷售數量排序）
		/// publisherIds = null 代表不限制；傳陣列則只統計該出版社(們)的書籍
		/// </summary>
		Task<IReadOnlyList<ChartPoint>> GetTopSoldBooksAsync(
			DateTime start,
			DateTime endInclusive,
			int topN = 10,
			int[]? excludeStatuses = null,
			int[]? publisherIds = null);

		/// <summary>
		/// 圓餅圖：近一段期間「借閱書籍」排行（以借閱次數計）
		/// publisherIds = null 代表不限制；傳陣列則只統計該出版社(們)
		/// </summary>
		Task<IReadOnlyList<ChartPoint>> GetTopBorrowBooksAsync(
			DateTime start,
			DateTime endInclusive,
			int topN = 10,
			int[]? publisherIds = null);
	}
}
