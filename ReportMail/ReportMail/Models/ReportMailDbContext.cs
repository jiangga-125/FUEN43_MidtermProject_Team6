using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ReportMail.Models;

public partial class ReportMailDbContext : DbContext
{
    public ReportMailDbContext()
    {
    }

    public ReportMailDbContext(DbContextOptions<ReportMailDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ReportAccessLog> ReportAccessLogs { get; set; }

    public virtual DbSet<ReportDefinition> ReportDefinitions { get; set; }

    public virtual DbSet<ReportExportLog> ReportExportLogs { get; set; }

    public virtual DbSet<ReportFilter> ReportFilters { get; set; }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //=> optionsBuilder.UseSqlServer("Server=(localdb)\\ProjectModels;Database=ReportMail;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportAccessLog>(entity =>
        {
            entity.HasKey(e => e.AccessId).HasName("PK__ReportAc__4130D0BF3C16D9EA");
            entity.ToTable("ReportAccessLog");

            entity.Property(e => e.AccessId).HasColumnName("AccessID");

            entity.Property(e => e.ActionType)
                  .HasMaxLength(32)         // ★ 原本 512 太大，而且不需要
                  .IsUnicode(true);

            entity.Property(e => e.Ipaddress)
                  .HasMaxLength(64)         // ★ 對應 DB: nvarchar(64)
                  .IsUnicode(true)
                  .HasColumnName("IPAddress");

            entity.Property(e => e.UserAgent)
                  .HasMaxLength(512)        // ★ 原本是 .HasMaxLength(1) 造成截斷
                  .IsUnicode(true);

            entity.Property(e => e.ReportDefinitionId).HasColumnName("ReportDefinitionID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.ReportDefinition).WithMany(p => p.ReportAccessLogs)
                  .HasForeignKey(d => d.ReportDefinitionId)
                  .HasConstraintName("FK__ReportAcc__Repor__00200768");
        });

        modelBuilder.Entity<ReportDefinition>(entity =>
        {
            entity.HasKey(e => e.ReportDefinitionId).HasName("PK__ReportDe__583078F1C0702258");
            entity.ToTable("ReportDefinition");

            entity.HasIndex(e => e.ReportName, "UQ__ReportDe__930D5CE7F30923DB").IsUnique();

            entity.Property(e => e.ReportDefinitionId).HasColumnName("ReportDefinitionID");

            entity.Property(e => e.Category)
                  .HasMaxLength(50)
                  .IsUnicode(true);          // ★ 若會有中文，改成 Unicode；不會中文可改回 false

            entity.Property(e => e.Description)
                  .HasMaxLength(500)         // ★ 原本 1 會被截斷
                  .IsUnicode(true);

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.ReportName)
                  .HasMaxLength(100)
                  .IsUnicode(true);          // ★ 允許中文名稱
        });

        modelBuilder.Entity<ReportExportLog>(entity =>
        {
            entity.HasKey(e => e.ExportId).HasName("PK__ReportEx__E5C997A4DB2D4DA0");
            entity.ToTable("ReportExportLog");

            entity.Property(e => e.ExportId)
                  //.ValueGeneratedNever()    // ★ 如果 DB 是 IDENTITY，請註解掉
                  .HasColumnName("ExportID");

            entity.Property(e => e.ExportFormat)
                  .HasMaxLength(20)
                  .IsUnicode(true);          // ★ 保守改成 Unicode

            entity.Property(e => e.FilePath)
                  .HasMaxLength(300)
                  .IsUnicode(true);          // ★ 路徑可能含非 ASCII

            entity.Property(e => e.ReportName)
                  .HasMaxLength(100)
                  .IsUnicode(true);          // ★ 允許中文

            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<ReportFilter>(entity =>
        {
            entity.HasKey(e => e.ReportFilterId).HasName("PK__ReportFi__93B7261003D11269");
            entity.ToTable("ReportFilter");

            entity.Property(e => e.ReportFilterId).HasColumnName("ReportFilterID");

            entity.Property(e => e.DataType)
                  .HasMaxLength(20)
                  .IsUnicode(false);

            entity.Property(e => e.Operator)
                  .HasMaxLength(20)
                  .IsUnicode(false);

            entity.Property(e => e.FieldName)
                  .HasMaxLength(100)
                  .IsUnicode(false);         // 英文欄位代碼，非 Unicode

            entity.Property(e => e.DisplayName)
                  .HasMaxLength(100)
                  .IsUnicode(true);

            entity.Property(e => e.DefaultValue)
                  .HasMaxLength(200)
                  .IsUnicode(true);

            entity.Property(e => e.Options)
                  .HasColumnType("nvarchar(max)"); // ★ 原本長度 1，存 JSON 會爆

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsRequired).HasDefaultValue(true);
            entity.Property(e => e.OrderIndex).HasDefaultValue(1);
            entity.Property(e => e.ReportDefinitionId).HasColumnName("ReportDefinitionID");

            entity.HasOne(d => d.ReportDefinition).WithMany(p => p.ReportFilters)
                  .HasForeignKey(d => d.ReportDefinitionId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK__ReportFil__Repor__7D439ABD");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
