using ReportMail.Data.Entities;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

public class FilterEditVM
{
    public int ReportFilterID { get; set; }
    public int ReportDefinitionID { get; set; }

    [Required, Display(Name = "欄位代碼")] public string FieldName { get; set; } = default!;
    [Required, Display(Name = "顯示名稱")] public string DisplayName { get; set; } = default!;
    [Required, Display(Name = "資料型態")] public string DataType { get; set; } = "string";
    [Required, Display(Name = "比較子")] public string Operator { get; set; } = "eq";
    [Display(Name = "預設值（JSON）")] public string? DefaultValueJson { get; set; }

    [Display(Name = "實際資料欄位（含別名）")] public string? Column { get; set; }
    [Display(Name = "選項 items（JSON）")] public string? ItemsJson { get; set; }

    [Display(Name = "顯示順序")] public int OrderIndex { get; set; } = 1;
    [Display(Name = "必填")] public bool IsRequired { get; set; }
    [Display(Name = "啟用")] public bool IsActive { get; set; } = true;

    public string BuildOptionsJson()
    {
        // 最小口徑：只存 column/items，QueryBuilder 只會用到 column
        var hasItems = !string.IsNullOrWhiteSpace(ItemsJson);
        if (hasItems) return $"{{\"column\":\"{Column}\",\"items\":{ItemsJson}}}";
        return $"{{\"column\":\"{Column}\"}}";
    }

    public static FilterEditVM FromEntity(ReportFilter f)
    {
        string? column = null, items = null;
        try
        {
            using var doc = JsonDocument.Parse(f.Options ?? "{}");
            var root = doc.RootElement;
            foreach (var p in root.EnumerateObject())
            {
                if (p.Name.Equals("column", StringComparison.OrdinalIgnoreCase))
                    column = p.Value.GetString();
                else if (p.NameEquals("items"))
                    items = p.Value.GetRawText();
            }
        }
        catch { /* ignore */ }

        return new FilterEditVM
        {
            ReportFilterID = f.ReportFilterID,
            ReportDefinitionID = f.ReportDefinitionID,
            FieldName = f.FieldName,
            DisplayName = f.DisplayName,
            DataType = f.DataType,
            Operator = f.Operator,
            DefaultValueJson = f.DefaultValue,
            Column = column,
            ItemsJson = items,
            OrderIndex = f.OrderIndex,
            IsRequired = f.IsRequired,
            IsActive = f.IsActive
        };
    }
}
