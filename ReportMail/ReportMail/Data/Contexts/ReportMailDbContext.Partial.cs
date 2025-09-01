using Microsoft.EntityFrameworkCore;

namespace ReportMail.Data.Contexts
{
    // 這是你的檔案（partial），re-scaffold 不會覆蓋
    public partial class ReportMailDbContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // 需要客製 Fluent API 設定再寫這裡（不改你的資料表）
        }
    }
}
