// Controllers/ReportsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;              // DbContext
using System.Globalization;
using ClosedXML.Excel;                       // 匯出
using ReportMail.Data.Entities;

namespace ReportMail.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ReportMailDbContext _db;
        public ReportsController(ReportMailDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var defs = await _db.Set<ReportDefinition>()      // 不靠 DbSet 名稱，直接用型別
                .AsNoTracking()
                .OrderBy(x => x.ReportName)                   // 有這欄就留著，沒有就拿掉
                .Select(x => new { Value = x.ReportDefinitionID, Text = x.ReportName })
                .ToListAsync();

            ViewBag.ReportDefs = defs;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Run(int reportDefinitionId, string? from, string? to)
        {
            try
            {
                var today = DateTime.Today;
                DateTime f = ParseDate(from) ?? new DateTime(today.Year, today.Month, 1);
                DateTime t = ParseDate(to) ?? today;

                // 依你的 DB 查詢銷售額（Orders/OrderDate/TotalAmount 皆來自你的 SQL）
                var points = await _db.DailySalesPoints.FromSqlRaw(@"
            SELECT CONVERT(date, o.OrderDate) AS [Day],
                   SUM(o.TotalAmount)         AS [Amount]
            FROM Orders o
            WHERE o.Status IN (1,2,3)                 -- 已付款/出貨/完成（依你定義）
              AND o.OrderDate >= {0}
              AND o.OrderDate <  DATEADD(day, 1, {1}) -- 含當日
            GROUP BY CONVERT(date, o.OrderDate)
            ORDER BY [Day]", f, t).ToListAsync();

                // 補齊空白日期
                var map = points.ToDictionary(x => x.Day, x => x.Amount);
                var labels = new List<string>();
                var data = new List<decimal>();
                for (var d = f.Date; d <= t.Date; d = d.AddDays(1))
                {
                    labels.Add(d.ToString("MM/dd"));
                    data.Add(map.TryGetValue(d, out var amt) ? amt : 0m);
                }

                return Json(new { labels, data, from = f.ToString("yyyy-MM-dd"), to = t.ToString("yyyy-MM-dd") });
            }
            catch (Exception ex)
            {
                // 方便你在瀏覽器看錯誤訊息（只建議開發環境用）
                Response.StatusCode = 500;
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int reportDefinitionId, string? from, string? to)
        {
            var today = DateTime.Today;
            DateTime f = ParseDate(from) ?? new DateTime(today.Year, today.Month, 1);
            DateTime t = ParseDate(to) ?? today;

            var points = await _db.DailySalesPoints.FromSqlRaw(@"
        SELECT CONVERT(date, o.OrderDate) AS [Day],
               SUM(o.TotalAmount)         AS [Amount]
        FROM Orders o
        WHERE o.Status IN (1,2,3)
          AND o.OrderDate >= {0}
          AND o.OrderDate <  DATEADD(day, 1, {1})
        GROUP BY CONVERT(date, o.OrderDate)
        ORDER BY [Day]", f, t).ToListAsync();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Sales");
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Amount";

            var map = points.ToDictionary(x => x.Day, x => x.Amount);
            int row = 2;
            for (var d = f.Date; d <= t.Date; d = d.AddDays(1))
            {
                ws.Cell(row, 1).Value = d;
                ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                ws.Cell(row, 2).Value = map.TryGetValue(d, out var amt) ? amt : 0m;
                row++;
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms); ms.Position = 0;
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Sales_{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
        }

        private static DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd", "MM/dd", "MM/dd/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(s, out dt)) return dt;
            return null;
        }
    }
}
