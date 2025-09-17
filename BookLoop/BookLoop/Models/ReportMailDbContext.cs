using System;
using System.Collections.Generic;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using ReportDefinition = BookLoop.Models.ReportDefinition;
using ReportFilter = BookLoop.Models.ReportFilter;

namespace BookLoop.Models;

public partial class ReportMailDbContext : DbContext
{
    public ReportMailDbContext(DbContextOptions<ReportMailDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ReportAccessLog> ReportAccessLogs { get; set; }

    public virtual DbSet<ReportDefinition> ReportDefinitions { get; set; }

    public virtual DbSet<ReportExportLog> ReportExportLogs { get; set; }

    public virtual DbSet<ReportFilter> ReportFilters { get; set; }


}
