using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookLoop.Services.Export
{
	public interface IExcelExporter
	{
		/// <summary>
		/// 將 (label, value) 序列匯出成 Excel 單張工作表。
		/// </summary>
		/// <param name="title">主標題（檔名也會用到）</param>
		/// <param name="subTitle">副標題（可空）</param>
		/// <param name="series">資料列：label=左欄、value=右欄</param>
		/// <param name="sheetName">工作表名稱</param>
		/// <param name="labelHeader">左欄欄名（例如：日期/年/月/日/書名/種類/項目）</param>
		/// <param name="valueHeader">右欄欄名（例如：銷售金額/本數/借閱次數/數值）</param>
		/// <returns>(bytes, fileName, contentType)</returns>
		Task<(byte[] bytes, string fileName, string contentType)> ExportSeriesAsync(
			string title,
			string subTitle,
			List<(string label, decimal value)> series,
			string sheetName,
			string labelHeader = "項目",
			string valueHeader = "數值"
		);
	}
}
