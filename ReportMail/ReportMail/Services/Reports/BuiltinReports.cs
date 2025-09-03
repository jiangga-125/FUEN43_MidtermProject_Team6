using System.Collections.Generic;
using System.Linq;

namespace ReportMail.Services.Reports
{
    // 保留負數 Id 給系統內建（永不落 DB）
    public enum BuiltinReportId
    {
        Line_TotalSales = -1, // 折線：總銷售金額（預設：日 / 近30日）
        Bar_TopSellingBooks = -2, // 長條：銷售書籍排行（預設：Top10 / 近30日）
        Pie_BorrowCategoryRank = -3, // 圓餅：借閱書籍種類排行（預設：Top5 / 近30日）
    }

    public record BuiltinReport(int Id, string Name, string ChartType); // ChartType: line/bar/pie

    public static class BuiltinReports
    {
        private static readonly BuiltinReport[] _all =
        {
            new((int)BuiltinReportId.Line_TotalSales,        "總銷售金額（系統預設）",     "line"),
            new((int)BuiltinReportId.Bar_TopSellingBooks,    "銷售書籍排行（系統預設）", "bar"),
            new((int)BuiltinReportId.Pie_BorrowCategoryRank, "借閱種類排行（系統預設）", "pie"),
        };

        public static IReadOnlyList<BuiltinReport> All => _all;
        public static bool IsBuiltin(int id) => _all.Any(x => x.Id == id);
        public static IEnumerable<BuiltinReport> ForChart(string chartType)
            => _all.Where(x => x.ChartType == chartType);
    }
}
