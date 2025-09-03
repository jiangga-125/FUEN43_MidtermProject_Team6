// Areas/ReportMail/Models/ReportFilters/FilterDesignerVM.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ReportMail.Models.ReportFilters
{
    /// <summary>
    /// 篩選欄位「設計表單」VM（只給 UI 用，不綁 EF）。
    /// 建立時填一次，送出後用 ExpandToRows() 產出 1~多筆 ReportFilter 要寫進資料庫的資料列。
    /// </summary>
    public class FilterDesignerVM
    {
        // ───────── 共同欄位 ─────────
        [Required, Display(Name = "報表定義 ID")]
        public int ReportDefinitionId { get; set; }

        [Required, Display(Name = "範本")]
        public FilterTemplateKind Template { get; set; } = FilterTemplateKind.DateRange;
        // 範本說明：
        // DateRange   → 產 StartDate(gte) / EndDate(lte) 兩列
        // SingleValue → 產 1 列（依 DataType + Operator 組 WHERE）
        // MultiSelect → 產 1 列（固定 multiselect + in）

        /// <summary>
        /// 實際查詢用欄位（可含別名）。QueryBuilder 會讀 Options.column 來組 WHERE。
        /// 例：o.OrderDate / q.RowNum / b.Price / YEAR(b.PublishDate)
        /// </summary>
        [Required, Display(Name = "資料來源欄位（含別名）")]
        public string Column { get; set; } = default!;

        [Display(Name = "顯示順序")]
        public int OrderIndex { get; set; } = 1;

        [Display(Name = "是否必填")]
        public bool IsRequired { get; set; } = false;

        [Display(Name = "是否啟用")]
        public bool IsActive { get; set; } = true;

        // ───────── 單值 / 多選 共用欄位 ─────────
        [Display(Name = "欄位代碼（URL 參數）")]
        public string? FieldName { get; set; }      // 例：Status / Category / PriceMin

        [Display(Name = "顯示名稱（UI 標籤）")]
        public string? DisplayName { get; set; }    // 例：狀態 / 書籍種類 / 單價下限

        /// <summary>date / int / decimal / string / boolean / select / multiselect</summary>
        [Display(Name = "資料型態")]
        public string? DataType { get; set; }

        /// <summary>eq / ne / gt / gte / lt / lte / like / in / between(如需)</summary>
        [Display(Name = "比較子")]
        public string? Operator { get; set; }

        /// <summary>預設值（JSON 字串），例：{"value":1} 或 {"values":["A","B"]}</summary>
        [Display(Name = "預設值（JSON）")]
        public string? DefaultValueJson { get; set; }

        /// <summary>多選/下拉的靜態項目（如需）。若要動態項目，可只給 Column，前端呼叫 API 取 distinct。</summary>
        [Display(Name = "選項（多選/下拉）")]
        public List<SelectItem> Items { get; set; } = new();

        // ───────── 日期區間專用（會產兩列） ─────────
        [Display(Name = "起始欄位代碼（URL）")] public string? StartFieldName { get; set; } = "StartDate";
        [Display(Name = "起始顯示名稱（UI）")] public string? StartDisplayName { get; set; } = "開始日期";
        [Display(Name = "結束欄位代碼（URL）")] public string? EndFieldName { get; set; } = "EndDate";
        [Display(Name = "結束顯示名稱（UI）")] public string? EndDisplayName { get; set; } = "結束日期";

        // ───────── 輔助型別 ─────────
        public class SelectItem
        {
            [Required] public string Value { get; set; } = default!;
            [Required] public string Text { get; set; } = default!;
        }

        /// <summary>
        /// 組 Options JSON。最小口徑只需 column；若有 Items 則一併放進去，前端可直接用來渲染下拉。
        /// </summary>
        public string BuildOptionsJson()
        {
            var obj = new Dictionary<string, object?> { ["column"] = Column };
            if (Items is { Count: > 0 })
                obj["items"] = Items.Select(x => new { value = x.Value, text = x.Text }).ToArray();
            return JsonSerializer.Serialize(obj);
        }

        /// <summary>
        /// 依範本展開成 1～多筆 ReportFilter 欲寫入的資料列（DTO）。
        /// Controller 迭代這些列，轉成 EF 實體後 Add + SaveChanges 即可。
        /// </summary>
        public IEnumerable<NewFilterRow> ExpandToRows()
        {
            var options = BuildOptionsJson();

            switch (Template)
            {
                case FilterTemplateKind.DateRange:
                    // 起始：>=
                    yield return new NewFilterRow(
                        FieldName: StartFieldName ?? "StartDate",
                        DisplayName: StartDisplayName ?? "開始日期",
                        DataType: "date",
                        Operator: "gte",
                        DefaultValueJson: null,
                        OptionsJson: options,
                        OrderIndex: OrderIndex,
                        IsRequired: IsRequired,
                        IsActive: IsActive
                    );
                    // 結束：<=
                    yield return new NewFilterRow(
                        FieldName: EndFieldName ?? "EndDate",
                        DisplayName: EndDisplayName ?? "結束日期",
                        DataType: "date",
                        Operator: "lte",
                        DefaultValueJson: null,
                        OptionsJson: options,
                        OrderIndex: OrderIndex + 1,
                        IsRequired: IsRequired,
                        IsActive: IsActive
                    );
                    break;

                case FilterTemplateKind.MultiSelect:
                    yield return new NewFilterRow(
                        FieldName: Require(FieldName, nameof(FieldName)),
                        DisplayName: Require(DisplayName, nameof(DisplayName)),
                        DataType: "multiselect",
                        Operator: "in",
                        DefaultValueJson: DefaultValueJson,
                        OptionsJson: options,
                        OrderIndex: OrderIndex,
                        IsRequired: IsRequired,
                        IsActive: IsActive
                    );
                    break;

                case FilterTemplateKind.SingleValue:
                default:
                    yield return new NewFilterRow(
                        FieldName: Require(FieldName, nameof(FieldName)),
                        DisplayName: Require(DisplayName, nameof(DisplayName)),
                        DataType: Require(DataType, nameof(DataType)), // 例：int/decimal/string/boolean/date/select
                        Operator: Operator ?? "eq",
                        DefaultValueJson: DefaultValueJson,
                        OptionsJson: options,
                        OrderIndex: OrderIndex,
                        IsRequired: IsRequired,
                        IsActive: IsActive
                    );
                    break;
            }

            static string Require(string? v, string name) =>
                !string.IsNullOrWhiteSpace(v) ? v : throw new ValidationException($"{name} 不可為空");
        }
    }

    /// <summary>展開後的一列資料（Controller 轉 EF 實體 ReportFilter 寫 DB）</summary>
    public record NewFilterRow(
        string FieldName,
        string DisplayName,
        string DataType,
        string Operator,
        string? DefaultValueJson,
        string OptionsJson,
        int OrderIndex,
        bool IsRequired,
        bool IsActive
    );

    /// <summary>篩選欄位範本種類</summary>
    public enum FilterTemplateKind
    {
        DateRange,    // 產兩列：StartDate(gte)、EndDate(lte)
        SingleValue,  // 產一列：依 DataType + Operator
        MultiSelect   // 產一列：固定 multiselect + in
    }
}
