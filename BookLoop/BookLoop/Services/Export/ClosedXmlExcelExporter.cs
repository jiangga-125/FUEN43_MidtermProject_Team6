using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace BookLoop.Services.Export
{
    public class ClosedXmlExcelExporter : IExcelExporter
    {
        public Task<(byte[] bytes, string fileName, string contentType)> ExportSeriesAsync(string title,
            string subTitle,
            List<(string label, decimal value)> series,
            string sheetName,
            string labelHeader = "項目",
            string valueHeader = "數值")
        {
            using var wb = new XLWorkbook();

            var safeSheet = MakeSafeSheetName(string.IsNullOrWhiteSpace(sheetName) ? "Report" : sheetName);
            var ws = wb.Worksheets.Add(safeSheet);

            int r = 1;

            // 標題區
            if (!string.IsNullOrWhiteSpace(title))
            {
                ws.Cell(r, 1).Value = title;
                ws.Range(r, 1, r, 2).Merge().Style.Font.SetBold().Font.FontSize = 14;
                r++;
            }
            if (!string.IsNullOrWhiteSpace(subTitle))
            {
                ws.Cell(r, 1).Value = subTitle;
                ws.Range(r, 1, r, 2).Merge().Style.Font.Italic = true;
                r++;
            }

            // 表頭
            ws.Cell(r, 1).Value = string.IsNullOrWhiteSpace(labelHeader) ? "項目" : labelHeader;
            ws.Cell(r, 2).Value = string.IsNullOrWhiteSpace(valueHeader) ? "數值" : valueHeader;
            ws.Range(r, 1, r, 2).Style.Font.SetBold();
            ws.Range(r, 1, r, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            r++;

            // 明細
            foreach (var (label, value) in series)
            {
                ws.Cell(r, 1).Value = label ?? string.Empty;
                ws.Cell(r, 2).Value = value;
                r++;
            }

            // 欄寬 & 數字格式
            ws.Columns(1, 2).AdjustToContents();
            ws.Column(2).Style.NumberFormat.Format = "#,##0.##";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var bytes = ms.ToArray();
            var fileName = MakeSafeFileName($"{(string.IsNullOrWhiteSpace(title) ? "Report" : title.Trim())}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return Task.FromResult((bytes, fileName, contentType));
        }

        private static string MakeSafeSheetName(string name)
        {
            var s = string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                s = s.Replace(ch.ToString(), "");
            if (s.Length > 31) s = s[..31];
            return string.IsNullOrWhiteSpace(s) ? "Sheet1" : s;
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }

        // 配合介面擴充；這裡「忽略 chartKind」直接呼叫舊的
        public Task<(byte[] bytes, string fileName, string contentType)> ExportSeriesAsync(
            string title,
            string subTitle,
            List<(string label, decimal value)> series,
            string sheetName,
            ChartKind chartKind,                 // 新增參數（不使用）
            string labelHeader = "項目",
            string valueHeader = "數值")
        {
            // 轉呼叫舊簽章，維持 ClosedXML 只輸出表格、不畫圖的行為
            return ExportSeriesAsync(title, subTitle, series, sheetName, labelHeader, valueHeader);
        }
    }
}
