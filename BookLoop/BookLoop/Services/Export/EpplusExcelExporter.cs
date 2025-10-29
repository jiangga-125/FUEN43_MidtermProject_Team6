using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookLoop.Services.Export
{
    /// <summary>
    /// 匯出單一工作表 + 單一資料序列的 Excel，並插入對應圖表（Column/Line/Pie）。
    /// 與 IExcelExporter 相容：舊簽章會自動用推斷邏輯決定折線或長條。
    /// </summary>
    public class EpplusExcelExporter : IExcelExporter
    {
        public async Task<(byte[] bytes, string fileName, string contentType)> ExportSeriesAsync(
            string title, string subTitle, List<(string label, decimal value)> series,
            string sheetName, string labelHeader = "項目", string valueHeader = "數值")
        {
            var kind = IsTimeAxis(labelHeader) ? ChartKind.Line : ChartKind.Column;
            return await ExportCoreAsync(title, subTitle, series, sheetName, kind, labelHeader, valueHeader);
        }

        public Task<(byte[] bytes, string fileName, string contentType)> ExportSeriesAsync(
            string title, string subTitle, List<(string label, decimal value)> series,
            string sheetName, ChartKind chartKind,
            string labelHeader = "項目", string valueHeader = "數值")
        {
            return ExportCoreAsync(title, subTitle, series, sheetName, chartKind, labelHeader, valueHeader);
        }

        private async Task<(byte[] bytes, string fileName, string contentType)> ExportCoreAsync(
            string title, string subTitle, List<(string label, decimal value)> series,
            string sheetName, ChartKind chartKind, string labelHeader, string valueHeader)
        {
            if (series == null || series.Count == 0)
                return (Array.Empty<byte>(), "empty.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            using var pkg = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add(SafeSheetName(sheetName));

            int r = 1;

            // 標題
            if (!string.IsNullOrWhiteSpace(title))
            {
                ws.Cells[r, 1].Value = title;
                ws.Cells[r, 1, r, 2].Merge = true;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 1].Style.Font.Size = 14;
                r++;
            }
            if (!string.IsNullOrWhiteSpace(subTitle))
            {
                ws.Cells[r, 1].Value = subTitle;
                ws.Cells[r, 1, r, 2].Merge = true;
                ws.Cells[r, 1].Style.Font.Italic = true;
                r++;
            }

            // 表頭
            ws.Cells[r, 1].Value = string.IsNullOrWhiteSpace(labelHeader) ? "項目" : labelHeader;
            ws.Cells[r, 2].Value = string.IsNullOrWhiteSpace(valueHeader) ? "數值" : valueHeader;
            ws.Cells[r, 1, r, 2].Style.Font.Bold = true;
            r++;

            // 明細
            foreach (var (label, value) in series)
            {
                ws.Cells[r, 1].Value = label ?? "";
                ws.Cells[r, 2].Value = value;
                r++;
            }
            int lastRow = r - 1;
            int firstDataRow = lastRow - (series.Count - 1);

            ws.Column(1).AutoFit();
            ws.Column(2).AutoFit();
            ws.Column(2).Style.Numberformat.Format = "#,##0.##";

            // 建圖
            var chartId = Guid.NewGuid().ToString("N");
            ExcelChart chart = chartKind switch
            {
                ChartKind.Line => ws.Drawings.AddChart(chartId, eChartType.Line) as ExcelLineChart,
                ChartKind.Pie => ws.Drawings.AddChart(chartId, eChartType.Pie) as ExcelPieChart,
                _ => ws.Drawings.AddChart(chartId, eChartType.ColumnClustered) as ExcelBarChart
            };
            chart.Title.Text = string.IsNullOrWhiteSpace(title) ? "圖表" : title;

            if (chartKind == ChartKind.Pie)
            {
                var cats = ws.Cells[firstDataRow, 1, lastRow, 1];
                var vals = ws.Cells[firstDataRow, 2, lastRow, 2];
                var s = chart.Series.Add(vals, cats);
                s.HeaderAddress = ws.Cells[firstDataRow - 1, 2]; // 表頭
            }
            else
            {
                var x = ws.Cells[firstDataRow, 1, lastRow, 1]; // 類別/時間
                var y = ws.Cells[firstDataRow, 2, lastRow, 2]; // 數值
                var s = chart.Series.Add(y, x);
                s.HeaderAddress = ws.Cells[firstDataRow - 1, 2];

                if (chart is ExcelChartStandard std)
                {
                    std.XAxis.Title.Text = labelHeader;
                    std.YAxis.Title.Text = valueHeader;
                }
            }

            // 擺放位置（資料右側），大小 800x400
            chart.SetPosition(1, 0, 3, 0);
            chart.SetSize(800, 400);

            // 美化：把資料變成 Excel Table（可篩選，視覺一致）
            ws.Tables.Add(ws.Cells[firstDataRow - 1, 1, lastRow, 2], $"{ws.Name}_Table");

            // 檔名與輸出
            var fileName = $"{SafeFileName(string.IsNullOrWhiteSpace(title) ? "Report" : title)}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
            var bytes = pkg.GetAsByteArray();
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            await Task.CompletedTask;
            return (bytes, fileName, contentType);
        }

        private static bool IsTimeAxis(string? header)
            => !string.IsNullOrWhiteSpace(header) &&
               (header.Contains("日期") || header.Contains("月份") || header.Contains("年份") || header.Contains("時間"));

        private static string SafeSheetName(string s)
        {
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' }) s = s.Replace(ch.ToString(), "");
            if (s.Length > 31) s = s[..31];
            return string.IsNullOrWhiteSpace(s) ? "Sheet1" : s;
        }

        private static string SafeFileName(string name)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return string.IsNullOrWhiteSpace(name) ? "Report" : name;
        }
    }
}
