// Services/Reports/ReportQueryBuilder.cs
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace ReportMail.Services.Reports
{
    /// <summary>
    /// 依「報表食譜（ReportRecipe）」＋ 使用者提交的 QueryString，
    /// 動態組出可供 FromSqlRaw 使用的 SQL 與對應參數清單。
    /// 輸出固定為兩欄：Label / Value（前端畫圖與匯出皆可共用）
    /// </summary>
    public class ReportQueryBuilder
    {
        /// <param name="recipe">從 ReportFilter 的 _source/_dimension/_metric/_preset 解析出的配置</param>
        /// <param name="filters">此報表的所有可用篩選欄位（不含底線開頭的 meta）</param>
        /// <param name="query">前端傳來的 QueryString（例如 StartDate=...&Category=A&Category=B）</param>
        public (string sql, List<SqlParameter> @params) Build(
            ReportRecipe recipe,
            IEnumerable<(string FieldName, string DataType, string Operator, string? Options)> filters,
            IQueryCollection query)
        {
            // 1) FROM：資料來源（可 table 或子查詢）
            string from = recipe.Source.Type.Equals("table", StringComparison.OrdinalIgnoreCase)
                ? $"FROM dbo.{recipe.Source.Name} {recipe.Source.Alias}"
                : $"FROM ({recipe.Source.Name}) {recipe.Source.Alias}";

            // 2) 維度（群組欄位/標籤/排序），支援 UI 覆蓋顆粒度 Granularity=day|month|year
            var granOverride = query["Granularity"].FirstOrDefault()?.Trim().ToLowerInvariant(); // 只用來覆蓋，不進 WHERE
            var (groupExpr, labelExpr, orderExpr) = BuildDimension(recipe, granOverride);

            // 3) WHERE 預設條件（_preset.where）
            var where = new StringBuilder();
            var ps = new List<SqlParameter>();
            if (!string.IsNullOrWhiteSpace(recipe.WherePreset))
                where.Append($" AND ({recipe.WherePreset})");

            // 4) 指標：固定輸出別名 Value（避免 FromSql 對不到欄位）
            string metricExpr = string.IsNullOrWhiteSpace(recipe.Metric.Expr) ? "COUNT(1)" : recipe.Metric.Expr;
            const string metricAlias = "Value";

            // 5) 動態篩選（依使用者輸入）
            int i = 0;
            foreach (var f in filters)
            {
                string field = f.FieldName;                                 // 例如：StartDate / EndDate / Category / RankFrom ...
                if (string.Equals(field, "Granularity", StringComparison.OrdinalIgnoreCase))
                {
                    // ⚠️ 顆粒度是給 BuildDimension 用，不應進 WHERE
                    continue;
                }

                string type = (f.DataType ?? "string").ToLowerInvariant();  // date/int/decimal/string/boolean/select/multiselect
                string op = (f.Operator ?? "eq").ToLowerInvariant();      // eq/ne/gt/gte/lt/lte/like/in...
                string col = ReadColumnFromOptions(f.Options) ?? field;    // 真正的資料欄位（可含別名）

                var inputs = query[field];
                if (inputs.Count == 0) continue;                            // 沒帶值就略過

                // 多選 vs 單值：multiselect 或 in → 多值；其餘取第一個
                var vals = (type == "multiselect" || op == "in")
                    ? inputs.ToArray().Where(s => !string.IsNullOrWhiteSpace(s))
                    : new[] { inputs[0] ?? string.Empty };

                // ───────────────── 型別分支 ─────────────────
                if (type == "date")
                {
                    // 允許 gte/lte/gt/lt/eq（日期字串直接轉 DateTime.Date）
                    var s = vals.FirstOrDefault();
                    if (DateTime.TryParse(s, out var dt))
                    {
                        var p = new SqlParameter($"@p{i++}", dt.Date);
                        where.Append($" AND {col} {ToSqlOp(op)} {p.ParameterName}");
                        ps.Add(p);
                    }
                }
                else if (type == "int" || type == "decimal")
                {
                    if (op == "in")
                    {
                        var names = new List<string>();
                        foreach (var s in vals)
                        {
                            if (!decimal.TryParse(s, out var dv)) continue; // int 也以 decimal 參數處理，SQL 會隱式轉型
                            var p = new SqlParameter($"@p{i++}", dv);
                            ps.Add(p); names.Add(p.ParameterName);
                        }
                        if (names.Count > 0) where.Append($" AND {col} IN ({string.Join(",", names)})");
                    }
                    else
                    {
                        var s = vals.FirstOrDefault();
                        if (decimal.TryParse(s, out var dv))
                        {
                            var p = new SqlParameter($"@p{i++}", dv);
                            where.Append($" AND {col} {ToSqlOp(op)} {p.ParameterName}");
                            ps.Add(p);
                        }
                    }
                }
                else if (type == "boolean")
                {
                    var s = vals.FirstOrDefault();
                    if (bool.TryParse(s, out var b))
                    {
                        var p = new SqlParameter($"@p{i++}", b);
                        where.Append($" AND {col} = {p.ParameterName}");
                        ps.Add(p);
                    }
                }
                else // string / select（注意：這裡也支援 > >= < <= 等比較子，方便「數值下拉」）
                {
                    if (op == "in")
                    {
                        var names = new List<string>();
                        foreach (var s in vals)
                        {
                            var p = new SqlParameter($"@p{i++}", s);
                            ps.Add(p); names.Add(p.ParameterName);
                        }
                        if (names.Count > 0) where.Append($" AND {col} IN ({string.Join(",", names)})");
                    }
                    else
                    {
                        var s = vals.FirstOrDefault() ?? string.Empty;
                        var p = new SqlParameter($"@p{i++}", op == "like" ? $"%{s}%" : s);
                        where.Append($" AND {col} {ToSqlOp(op)} {p.ParameterName}");
                        ps.Add(p);
                    }
                }
            }

            // 6) 最終 SQL：固定輸出 Label / Value
            var sql = $@"
SELECT {labelExpr} AS Label, {metricExpr} AS {metricAlias}
{from}
WHERE 1=1{where}
GROUP BY {groupExpr}
ORDER BY {orderExpr}";

            return (sql, ps);
        }

        /// <summary>
        /// 產生 Group/Label/Order 字串。
        /// 對日期維度支援 granOverride（day/month/year），若未指定則用 recipe 預設。
        /// </summary>
        private static (string groupExpr, string labelExpr, string orderExpr)
            BuildDimension(ReportRecipe r, string? granOverride = null)
        {
            // 已含別名就不再加，避免 o.o.OrderDate
            var col = (r.Dimension.Column ?? "").Trim();
            var c = col.Contains('.') ? col : $"{r.Source.Alias}.{col}";

            if (r.Dimension.Type.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                var g = (granOverride ?? r.Dimension.Granularity ?? "day").ToLowerInvariant();

                if (g == "day")
                {
                    var group = $"CONVERT(date, {c})";
                    var label = $"CONVERT(nvarchar(10), CONVERT(date, {c}), 120)"; // yyyy-MM-dd
                    return (group, label, group);
                }
                if (g == "month")
                {
                    var group = $"FORMAT({c}, 'yyyy-MM')";
                    return (group, group, group);
                }
                if (g == "year")
                {
                    var group = $"DATEPART(year, {c})";
                    return (group, group, group);
                }
            }

            // 非日期：直接用欄位
            return (c, c, c);
        }

        /// <summary>
        /// 讀 Options JSON 的 column 欄位（大小寫不敏感）。找不到就回 null。
        /// </summary>
        private static string? ReadColumnFromOptions(string? options)
        {
            if (string.IsNullOrWhiteSpace(options)) return null;

            try
            {
                using var doc = JsonDocument.Parse(options);
                var root = doc.RootElement;

                // 先嘗試精確名稱
                if (root.TryGetProperty("column", out var col)) return col.GetString();

                // 大小寫不敏感搜尋
                foreach (var p in root.EnumerateObject())
                {
                    if (p.Name.Equals("column", StringComparison.OrdinalIgnoreCase))
                        return p.Value.GetString();
                }
            }
            catch
            {
                // 無效 JSON 就當作無欄位，交由呼叫端用 FieldName
            }
            return null;
        }

        /// <summary>
        /// 將自訂的 Operator（eq/ne/gt/gte/lt/lte/like）轉為 SQL 符號。
        /// 未知一律當 eq。
        /// </summary>
        private static string ToSqlOp(string op) => op switch
        {
            "ne" => "<>",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            "like" => "LIKE",
            _ => "=" // eq 或未指定
        };
    }
}
