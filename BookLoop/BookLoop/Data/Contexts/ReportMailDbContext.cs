using System;
using System.Collections.Generic;
using BookLoop.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Entities;
using ReportDefinition = BookLoop.Data.Entities.ReportDefinition;
using ReportFilter = BookLoop.Data.Entities.ReportFilter;

namespace BookLoop.Data.Contexts;

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
