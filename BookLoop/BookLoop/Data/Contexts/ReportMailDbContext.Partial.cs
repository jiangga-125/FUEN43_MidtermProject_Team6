using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Data.Contexts
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
    }
}
