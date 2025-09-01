// Services/Reports/ReportQueryBuilder.cs
using System.Text;
using System.Text.Json;
using System.Linq;                 // ← 需要
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace ReportMail.Services.Reports
{
    public class ReportQueryBuilder
    {
        public (string sql, List<SqlParameter> @params) Build(
            ReportRecipe recipe,
            IEnumerable<(string FieldName, string DataType, string Operator, string? Options)> filters,
            IQueryCollection query)
        {
            // FROM
            string from = recipe.Source.Type.Equals("table", StringComparison.OrdinalIgnoreCase)
                ? $"FROM dbo.{recipe.Source.Name} {recipe.Source.Alias}"
                : $"FROM ({recipe.Source.Name}) {recipe.Source.Alias}";

            // 維度（群組欄位與標籤欄位）
            var (groupExpr, labelExpr, orderExpr) = BuildDimension(recipe);

            // 指標
            string metricExpr = string.IsNullOrWhiteSpace(recipe.Metric.Expr) ? "COUNT(1)" : recipe.Metric.Expr;
            string metricAlias = string.IsNullOrWhiteSpace(recipe.Metric.Alias) ? "Value" : recipe.Metric.Alias;

            // WHERE
            var where = new StringBuilder();
            var ps = new List<SqlParameter>();
            if (!string.IsNullOrWhiteSpace(recipe.WherePreset))
                where.Append($" AND ({recipe.WherePreset})");

            int i = 0;
            foreach (var f in filters)
            {
                string field = f.FieldName;                       // StartDate / EndDate / Status...
                string type = (f.DataType ?? "string").ToLowerInvariant();
                string op = (f.Operator ?? "eq").ToLowerInvariant();
                string col = ReadColumnFromOptions(f.Options) ?? field; // 實際欄位名（可含別名）

                var inputs = query[field];
                if (inputs.Count == 0) continue;

                // 多選 vs 單值
                var vals = (type == "multiselect" || op == "in")
                    ? inputs.ToArray().Where(s => !string.IsNullOrEmpty(s))
                    : new[] { inputs[0] ?? string.Empty };

                // -------- 正確的型別分支 --------
                if (type == "date")
                {
                    if (DateTime.TryParse(vals.First(), out var dt))
                    {
                        var p = new SqlParameter($"@p{i++}", dt.Date);
                        where.Append(op switch
                        {
                            "gte" => $" AND {col} >= {p.ParameterName}",
                            "lte" => $" AND {col} <= {p.ParameterName}",
                            "gt" => $" AND {col} >  {p.ParameterName}",
                            "lt" => $" AND {col} <  {p.ParameterName}",
                            _ => $" AND {col} =  {p.ParameterName}",
                        });
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
                            if (!decimal.TryParse(s, out var dv)) continue;
                            var p = new SqlParameter($"@p{i++}", dv);
                            ps.Add(p); names.Add(p.ParameterName);
                        }
                        if (names.Count > 0) where.Append($" AND {col} IN ({string.Join(",", names)})");
                    }
                    else
                    {
                        var s = vals.First();
                        if (decimal.TryParse(s, out var dv))
                        {
                            var p = new SqlParameter($"@p{i++}", dv);
                            var sop = op switch { "ne" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=", _ => "=" };
                            where.Append($" AND {col} {sop} {p.ParameterName}");
                            ps.Add(p);
                        }
                    }
                }
                else if (type == "boolean")
                {
                    if (bool.TryParse(vals.First(), out var b))
                    {
                        var p = new SqlParameter($"@p{i++}", b);
                        where.Append($" AND {col} = {p.ParameterName}");
                        ps.Add(p);
                    }
                }
                else // string / select
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
                        var s = vals.First();
                        var p = new SqlParameter($"@p{i++}", op == "like" ? $"%{s}%" : s);
                        var sop = op == "like" ? "LIKE" : (op == "ne" ? "<>" : "=");
                        where.Append($" AND {col} {sop} {p.ParameterName}");
                        ps.Add(p);
                    }
                }
            }

            // 最終 SQL：固定輸出 Label/Value
            var sql = $@"
SELECT {labelExpr} AS Label, {metricExpr} AS {metricAlias}
{from}
WHERE 1=1{where}
GROUP BY {groupExpr}
ORDER BY {orderExpr}";

            return (sql, ps);
        }

        private static (string groupExpr, string labelExpr, string orderExpr) BuildDimension(ReportRecipe r)
        {
            // 已含別名就不再加，避免 o.o.OrderDate
            var col = (r.Dimension.Column ?? "").Trim();
            var c = col.Contains('.') ? col : $"{r.Source.Alias}.{col}";

            if (r.Dimension.Type.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                var g = (r.Dimension.Granularity ?? "day").ToLowerInvariant();
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

        private static string? ReadColumnFromOptions(string? options)
        {
            if (string.IsNullOrWhiteSpace(options)) return null;
            try
            {
                using var doc = JsonDocument.Parse(options);
                if (doc.RootElement.TryGetProperty("column", out var col))
                    return col.GetString();
            }
            catch { }
            return null;
        }
    }
}
