using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Entities;

namespace ReportMail.Data.Contexts
{
    public partial class ReportMailDbContext
    {
        // 查詢用 DbSet（不會建表）
        public virtual DbSet<ChartPoint> ChartPoints => Set<ChartPoint>();

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Keyless，且不對應任何資料表/檢視
            modelBuilder.Entity<ChartPoint>(e =>
            {
                e.HasNoKey();
                e.ToView(null); // 告訴 EF 這不是 table/view
            });
        }
    }
}
