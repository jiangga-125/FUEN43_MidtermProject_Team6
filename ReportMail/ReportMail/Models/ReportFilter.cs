using System;
using System.Collections.Generic;

namespace ReportMail.Models;

public partial class ReportFilter
{
    public int ReportFilterId { get; set; }

    public int ReportDefinitionId { get; set; }

    public string FieldName { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string DataType { get; set; } = null!;

    public string Operator { get; set; } = null!;

    public string? DefaultValue { get; set; }

    public string? Options { get; set; }

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ReportDefinition ReportDefinition { get; set; } = null!;
}
