using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Models
{
    public partial class ReportMailDbContext
    {
        public override int SaveChanges()
        {
            ApplyTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyTimestamps()
        {
            var now = DateTime.Now; // 本地時間
            foreach (var entry in ChangeTracker.Entries().Where(e =>
                         e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                var hasCreated = entry.Metadata.FindProperty("CreatedAt") != null;
                var hasUpdated = entry.Metadata.FindProperty("UpdatedAt") != null;

                if (entry.State == EntityState.Added)
                {
                    if (hasCreated) entry.Property("CreatedAt").CurrentValue = now;
                    if (hasUpdated) entry.Property("UpdatedAt").CurrentValue = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // 鎖住 CreatedAt，不允許被覆寫
                    if (hasCreated) entry.Property("CreatedAt").IsModified = false;
                    if (hasUpdated) entry.Property("UpdatedAt").CurrentValue = now;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ReportExportLog>(e =>
            {
                e.ToTable("ReportExportLog");
                e.HasKey(x => x.ExportID);

                e.Property(x => x.Category).HasMaxLength(20).IsRequired();
                e.Property(x => x.ReportName).HasMaxLength(200);
                e.Property(x => x.Format).HasMaxLength(10).IsRequired();
                e.Property(x => x.TargetEmail).HasMaxLength(320).IsRequired();
                e.Property(x => x.AttachmentFileName).HasMaxLength(260).IsRequired();
                e.Property(x => x.ErrorMessage).HasMaxLength(1000);
                e.Property(x => x.PolicyUsed).HasMaxLength(100);
                e.Property(x => x.Ip).HasMaxLength(45);
                e.Property(x => x.UserAgent).HasMaxLength(256);

                e.HasIndex(x => new { x.UserID, x.RequestedAt })
                 .HasDatabaseName("IX_ReportExportLog_User_RequestedAt");
                e.HasIndex(x => new { x.TargetEmail, x.RequestedAt })
                 .HasDatabaseName("IX_ReportExportLog_Email_RequestedAt");
                e.HasIndex(x => new { x.DefinitionID, x.RequestedAt })
                 .HasDatabaseName("IX_ReportExportLog_Def_RequestedAt");
                e.HasIndex(x => new { x.SupplierID, x.RequestedAt })
                 .HasDatabaseName("IX_ReportExportLog_Supplier_RequestedAt");

                // 若你有做 FK，可在這裡補 HasOne(...)。
            });
        }
    }
}
