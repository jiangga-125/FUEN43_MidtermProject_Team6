using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Entities;

namespace ReportMail.Data.Contexts
{
    public partial class ReportMailDbContext
    {
        // 讓 FromSqlRaw 有型別可以投射
        public virtual DbSet<ChartPoint> ChartPoints => Set<ChartPoint>();

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Keyless DTO 映射（不對應資料表）
            modelBuilder.Entity<ChartPoint>().HasNoKey();
        }
    }
}
