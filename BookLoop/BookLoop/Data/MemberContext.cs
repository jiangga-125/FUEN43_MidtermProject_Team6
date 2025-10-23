using BookLoop.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace BookLoop.Data;

public partial class MemberContext : DbContext
{

    public MemberContext(DbContextOptions<MemberContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<MemberCoupon> MemberCoupons { get; set; }

    public virtual DbSet<MemberPoint> MemberPoints { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderCouponSnapshot> OrderCouponSnapshots { get; set; }

    public virtual DbSet<PointsLedger> PointsLedgers { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<ReviewModeration> ReviewModerations { get; set; }

    public virtual DbSet<ReviewRuleSettings> ReviewRuleSettings { get; set; }

    public virtual DbSet<RuleApplication> RuleApplications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)

    public virtual DbSet<CouponCategory> CouponCategories { get; set; } = default!;

    public virtual DbSet<Category> Categories { get; set; } = default!;

    {
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(e => new { e.StartAt, e.EndAt }, "IX_Coupons_Date");

            entity.HasIndex(e => e.IsActive, "IX_Coupons_IsActive");

            entity.HasIndex(e => e.Code, "UX_Coupons_Code").IsUnique();

            entity.Property(e => e.CouponId).HasColumnName("CouponID");
            entity.Property(e => e.Code).HasMaxLength(32);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(400);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.RequireLogin).HasDefaultValue(true);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("trg_Members_Update"));

            entity.HasIndex(e => e.Username, "IX_Members_Username");

            //entity.HasIndex(e => e.Account, "UQ_Members_Account").IsUnique();

            entity.HasIndex(e => e.UserID, "UX_Members_UserID")
                .IsUnique()
                .HasFilter("([UserID] IS NOT NULL)");

            entity.Property(e => e.MemberID).HasColumnName("MemberID");
            //entity.Property(e => e.Account).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.UserID).HasColumnName("UserID");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<MemberCoupon>(entity =>
        {
            entity.HasIndex(e => e.CouponId, "IX_MemberCoupons_Coupon");

            entity.HasIndex(e => e.MemberId, "IX_MemberCoupons_Member");

            entity.HasIndex(e => e.Status, "IX_MemberCoupons_Status");

            entity.HasIndex(e => new { e.MemberId, e.CouponId }, "UX_MemberCoupons_Member_Coupon_OnlyOneUnused")
                .IsUnique()
                .HasFilter("([Status]=(0))");

            entity.HasIndex(e => new { e.MemberId, e.CouponId }, "UX_MemberCoupons_Member_Coupon_Status0")
                .IsUnique()
                .HasFilter("([Status]=(0))");

            entity.HasIndex(e => e.RedeemTxnId, "UX_MemberCoupons_RedeemTxnId_NotNull")
                .IsUnique()
                .HasFilter("([RedeemTxnId] IS NOT NULL)");

            entity.Property(e => e.MemberCouponId).HasColumnName("MemberCouponID");
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CouponId).HasColumnName("CouponID");
            entity.Property(e => e.MemberId).HasColumnName("MemberID");
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Coupon).WithMany(p => p.MemberCoupons)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberCoupons_Coupons");
        });

		modelBuilder.Entity<MemberPoint>(entity =>
		{
			entity.ToTable("MemberPoints", tb => tb.HasTrigger("trg_MemberPoints_UpdateTime"));

			// 主鍵：MemberPointID (獨立流水號，不要再映射成 MemberID)
			entity.HasKey(e => e.MemberPointID);

			// 基本欄位設定
			entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
			entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

			// 關聯：一個 Member 可以有多筆 MemberPoint
			entity.HasOne(d => d.Member)
				  .WithMany(p => p.MemberPoints)   // 改成 WithMany
				  .HasForeignKey(d => d.MemberID) // FK 是 MemberID
				  .OnDelete(DeleteBehavior.ClientSetNull)
				  .HasConstraintName("FK_MemberPoints_Members");
		});


		modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.MemberID, "IX_Orders_MemberID");

            entity.HasIndex(e => e.OrderDate, "IX_Orders_OrderDate");

            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CouponNameSnap).HasMaxLength(100);
            entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DiscountCode).HasMaxLength(50);
            entity.Property(e => e.MemberCouponID).HasColumnName("MemberCouponID");
            entity.Property(e => e.MemberID).HasColumnName("MemberID");
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.OrderDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.MemberCoupon).WithMany(p => p.Orders)
                .HasForeignKey(d => d.MemberCouponID)
                .HasConstraintName("FK_Orders_MemberCoupons");

            entity.HasOne(d => d.Member).WithMany(p => p.Orders)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Members");
        });

        modelBuilder.Entity<OrderCouponSnapshot>(entity =>
        {
            entity.HasKey(e => e.OrderCouponSnapID);

            entity.Property(e => e.OrderCouponSnapID).HasColumnName("OrderCouponSnapID");
            entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CouponID).HasColumnName("CouponID");
            entity.Property(e => e.CouponNameSnap).HasMaxLength(100);
            entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.MemberCouponID).HasColumnName("MemberCouponID");
            entity.Property(e => e.OrderID).HasColumnName("OrderID");

            entity.HasOne(d => d.Coupon).WithMany(p => p.OrderCouponSnapshots)
                .HasForeignKey(d => d.CouponID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderCouponSnapshots_Coupons");

            entity.HasOne(d => d.MemberCoupon).WithMany(p => p.OrderCouponSnapshots)
                .HasForeignKey(d => d.MemberCouponID)
                .HasConstraintName("FK_OrderCouponSnapshots_MemberCoupons");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderCouponSnapshots)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderCouponSnapshots_Orders");
        });

        modelBuilder.Entity<PointsLedger>(entity =>
        {
            entity.HasKey(e => e.PointsLedgerID);

            entity.ToTable("PointsLedger", tb => tb.HasTrigger("trg_PointsLedger_MaintainMemberPoints"));

            entity.HasIndex(e => e.CreatedAt, "IX_PointsLedger_CreatedAt");

            entity.HasIndex(e => e.MemberID, "IX_PointsLedger_MemberID");

            entity.HasIndex(e => e.OrderId, "IX_PointsLedger_OrderID");

            entity.HasIndex(e => new { e.MemberID, e.ExternalOrderNo }, "UX_PointsLedger_Member_External")
                .IsUnique()
                .HasFilter("([ExternalOrderNo] IS NOT NULL)");

            entity.Property(e => e.PointsLedgerID).HasColumnName("LedgerID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetimeoffset())");
            entity.Property(e => e.ExternalOrderNo).HasMaxLength(64);
            entity.Property(e => e.MemberID).HasColumnName("MemberID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ReasonCode).HasMaxLength(50);

            entity.HasOne(d => d.Member).WithMany(p => p.PointsLedgers)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PointsLedger_Member");

            entity.HasOne(d => d.Order).WithMany(p => p.PointsLedgers)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_PointsLedger_Order");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Reviews__74BC79AE20CAC39C");

            entity.HasIndex(e => new { e.MemberId, e.CreatedAt }, "IX_Reviews_Member_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_Reviews_Status_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.TargetType, e.TargetId, e.CreatedAt }, "IX_Reviews_Target").IsDescending(false, false, true);

            entity.Property(e => e.ReviewId).HasColumnName("ReviewID");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ImageUrls).HasMaxLength(2000);
            entity.Property(e => e.MemberId).HasColumnName("MemberID");
            entity.Property(e => e.TargetId).HasColumnName("TargetID");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<ReviewModeration>(entity =>
        {
            entity.HasKey(e => e.ModerationId).HasName("PK__ReviewMo__7817E6DFCF3BB95F");

            entity.HasIndex(e => e.ReviewId, "IX_ReviewModerations_ReviewID");

            entity.Property(e => e.ModerationId).HasColumnName("ModerationID");
            entity.Property(e => e.Reasons).HasMaxLength(2000);
            entity.Property(e => e.ReviewId).HasColumnName("ReviewID");
            entity.Property(e => e.ReviewedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<ReviewRuleSettings>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.BlockSelfReview).HasDefaultValue(true);
            entity.Property(e => e.DuplicatePolicy).HasDefaultValue((byte)1);
            entity.Property(e => e.DuplicateWindowHours).HasDefaultValue(24);
            entity.Property(e => e.ForbidUrls).HasDefaultValue(true);
            entity.Property(e => e.MinContentLength).HasDefaultValue(10);
            entity.Property(e => e.RatingMax).HasDefaultValue((byte)5);
            entity.Property(e => e.RatingMin).HasDefaultValue((byte)1);
            entity.Property(e => e.TargetTypeForMember).HasDefaultValue((byte)2);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<RuleApplication>(entity =>
        {
            entity.HasKey(e => e.RuleApplicationID).HasName("PK__RuleAppl__70448604C9545B62");

            entity.HasIndex(e => new { e.CouponCodeSnap, e.CreatedAt }, "IX_RuleApplications_Coupon_Time").IsDescending(false, true);

            entity.HasIndex(e => new { e.MemberID, e.CreatedAt }, "IX_RuleApplications_Member_Time").IsDescending(false, true);

            entity.HasIndex(e => e.ExternalOrderNo, "UX_RuleApplications_External").IsUnique();

            entity.Property(e => e.RuleApplicationID).HasColumnName("RuleAppID");
            entity.Property(e => e.CouponCodeSnap).HasMaxLength(32);
            entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ExternalOrderNo).HasMaxLength(64);
            entity.Property(e => e.MemberID).HasColumnName("MemberID");
            entity.Property(e => e.Payable).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("APPLIED");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Member).WithMany(p => p.RuleApplications)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RuleApps_Members");
        });

		modelBuilder.Entity<Category>(e =>
		{
			e.ToTable("Categories", "dbo"); // ← 對方的 schema/表名
											// 如果 PK 不是慣用名字，可補 e.HasKey(x => x.CategoryID);
											// 不想讓遷移去動到對方表，可加：
			e.ToTable(tb => tb.ExcludeFromMigrations());
		});
		modelBuilder.Entity<Coupon>(e => { e.ToTable("Coupons", "dbo"); });

		modelBuilder.Entity<CouponCategory>(e =>
		{
			e.ToTable("CouponCategories", "dbo");
			e.HasKey(x => x.CouponCategoryID);

			e.HasOne(x => x.Coupon)
			 .WithMany(c => c.CouponCategories)
			 .HasForeignKey(x => x.CouponID)
			 .OnDelete(DeleteBehavior.Cascade);

			e.HasOne(x => x.Category)
			 .WithMany(cat => cat.CouponCategories)
			 .HasForeignKey(x => x.CategoryID)
			 .OnDelete(DeleteBehavior.Cascade);

			e.HasIndex(x => x.CouponID).HasDatabaseName("IX_CouponCategories_CouponID");
			e.HasIndex(x => x.CategoryID).HasDatabaseName("IX_CouponCategories_CategoryID");

			e.HasIndex(x => new { x.CouponID, x.CategoryID })
			 .IsUnique()
			 .HasDatabaseName("UX_CouponCategories_Coupon_Category");
		});


		OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
