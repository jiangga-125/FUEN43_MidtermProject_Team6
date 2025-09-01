using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Entities;

namespace ReportMail.Data.Contexts;

public partial class ReportMailDbContext : DbContext
{
    public ReportMailDbContext(DbContextOptions<ReportMailDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EventOutbox> EventOutboxes { get; set; }

    public virtual DbSet<MailAttachment> MailAttachments { get; set; }

    public virtual DbSet<MailEventType> MailEventTypes { get; set; }

    public virtual DbSet<MailHistory> MailHistories { get; set; }

    public virtual DbSet<MailLog> MailLogs { get; set; }

    public virtual DbSet<MailQueue> MailQueues { get; set; }

    public virtual DbSet<MailRecipient> MailRecipients { get; set; }

    public virtual DbSet<MailSegment> MailSegments { get; set; }

    public virtual DbSet<MailTemplate> MailTemplates { get; set; }

    public virtual DbSet<MailTrigger> MailTriggers { get; set; }

    public virtual DbSet<MailTriggerSegment> MailTriggerSegments { get; set; }

    public virtual DbSet<MemberSubscription> MemberSubscriptions { get; set; }

    public virtual DbSet<ReportAccessLog> ReportAccessLogs { get; set; }

    public virtual DbSet<ReportDefinition> ReportDefinitions { get; set; }

    public virtual DbSet<ReportExportLog> ReportExportLogs { get; set; }

    public virtual DbSet<ReportFilter> ReportFilters { get; set; }

    public virtual DbSet<TriggerRun> TriggerRuns { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailAttachment>(entity =>
        {
            entity.HasOne(d => d.MailQueue).WithMany(p => p.MailAttachments).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailQueue>(entity =>
        {
            entity.HasOne(d => d.Template).WithMany(p => p.MailQueues).OnDelete(DeleteBehavior.SetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
