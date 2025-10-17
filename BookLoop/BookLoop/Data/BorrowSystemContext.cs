using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;


public partial class BorrowSystemContext : DbContext
{
    public BorrowSystemContext()
    {
    }

    public BorrowSystemContext(DbContextOptions<BorrowSystemContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BorrowRecord> BorrowRecords { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Listing> Listings { get; set; }

    public virtual DbSet<ListingAuthor> ListingAuthors { get; set; }

    public virtual DbSet<ListingImage> ListingImages { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<PenaltyRule> PenaltyRules { get; set; }

    public virtual DbSet<PenaltyTransaction> PenaltyTransactions { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

	public virtual DbSet<UsedBookInventory> UsedBookInventories { get; set; } = null!;
	public virtual DbSet<Branch> Branches { get; set; } = null!;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:BookLoop");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BorrowRecord>(entity =>
        {
            entity.HasKey(e => e.RecordID).HasName("PK__BorrowRe__FBDF78C9033C073A");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.StatusCode).HasDefaultValue((byte)1);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Listing).WithMany(p => p.BorrowRecords)
                .HasForeignKey(d => d.ListingID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BorrowRecords_Listings");

            entity.HasOne(d => d.Member).WithMany(p => p.BorrowRecords)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BorrowRecords_Members");
            entity.HasOne(d => d.Reservation)
            .WithOne(p => p.BorrowRecord)
            .HasForeignKey<BorrowRecord>(d => d.ReservationID)
            .HasConstraintName("FK_BorrowRecords_Reservations");

        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryID).HasName("PK__Categori__19093A2B45575A8A");

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Listing>(entity =>
        {
            entity.HasKey(e => e.ListingID).HasName("PK__Listings__BF3EBEF05A1A56C9");

            entity.Property(e => e.Condition).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ISBN)
                .HasMaxLength(13)
                .IsUnicode(false);
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.Title).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.Listings)
                .HasForeignKey(d => d.CategoryID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Listings_Categories");

            entity.HasOne(d => d.Publisher).WithMany(p => p.Listings)
                .HasForeignKey(d => d.PublisherID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Listings_Publishers");
        });

        modelBuilder.Entity<ListingAuthor>(entity =>
        {
            entity.HasKey(e => e.ListingAuthorID).HasName("PK__ListingA__099ADDEDBCEDAB1C");

            entity.HasIndex(e => new { e.ListingID, e.AuthorName }, "UQ_ListingAuthors_ListingID_AuthorName").IsUnique();
            // 1 本書 + 作者姓名 不能重複
            entity.HasIndex(e => e.ListingID, "UQ_ListingAuthors_OnePrimary")
                .IsUnique()
                .HasFilter("([IsPrimary]=(1))");
            // 每本書最多 1 位主作者（IsPrimary = 1）

            entity.Property(e => e.AuthorName).HasMaxLength(50);

            entity.HasOne(d => d.Listing).WithMany(p => p.ListingAuthors)
                .HasForeignKey(d => d.ListingID)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ListingAuthors_Listings_ListingID");
            //.HasConstraintName("FK_ListingAuthors_Listings");
        });

        modelBuilder.Entity<ListingImage>(entity =>
        {
            entity.HasKey(e => e.ImageID).HasName("PK__ListingI__7516F4EC90D4E052");

            entity.Property(e => e.Caption).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasDefaultValue("https://tse3.mm.bing.net/th/id/OIP.XMwbPt7RTqfSnle_yZFvywHaHa?pid=Api");

            entity.HasOne(d => d.Listing).WithMany(p => p.ListingImages)
                .HasForeignKey(d => d.ListingID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ListingImages_Listings");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.MemberID).HasName("PK__Members__0CF04B389121DEBC");

            entity.HasIndex(e => e.Account, "UX_Members_Account").IsUnique();

            entity.HasIndex(e => e.UserID, "UX_Members_UserID_NotNull")
                .IsUnique()
                .HasFilter("([UserID] IS NOT NULL)");

            entity.Property(e => e.Account).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<PenaltyRule>(entity =>
        {
            entity.HasKey(e => e.PenaltyRuleID).HasName("PK__PenaltyR__110458C22A9F1E42");

            entity.Property(e => e.ChargeType).HasMaxLength(20);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ReasonCode).HasMaxLength(100);
        });

        modelBuilder.Entity<PenaltyTransaction>(entity =>
        {
            entity.HasKey(e => e.PenaltyTransactionID).HasName("PK__PenaltyT__567E06E7E1D099D2");

            entity.HasIndex(e => e.MemberID, "IX_PenaltyTransactions_MemberID");

            entity.HasIndex(e => e.RecordID, "IX_PenaltyTransactions_RecordID");

            entity.HasIndex(e => e.RuleID, "IX_PenaltyTransactions_RuleID");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Member).WithMany(p => p.PenaltyTransactions)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PenaltyTransactions_Member");

            entity.HasOne(d => d.Record).WithMany(p => p.PenaltyTransactions)
                .HasForeignKey(d => d.RecordID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PenaltyTransactions_Record");

            entity.HasOne(d => d.Rule).WithMany(p => p.PenaltyTransactions)
                .HasForeignKey(d => d.RuleID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PenaltyTransactions_Rule");
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.PublisherID).HasName("PK__Publishe__4C657E4B0F107BE5");

            entity.HasIndex(e => e.PublisherName, "UQ__Publishe__5F0E2249140481EF").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PublisherName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationID).HasName("PK__Reservat__B7EE5F04B27A3B1A");

            entity.HasIndex(e => e.ListingID, "IX_Reservations_ListingID");

            entity.HasIndex(e => new { e.ListingID, e.Status }, "IX_Reservations_Listing_Status");

            entity.HasIndex(e => e.MemberID, "IX_Reservations_MemberID");

            entity.HasIndex(e => e.Status, "IX_Reservations_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Listing).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.ListingID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservations_Listings");

            entity.HasOne(d => d.Member).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.MemberID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservations_Members");
        });

		modelBuilder.Entity<UsedBookInventory>(e =>
		{
			e.ToTable("UsedBookInventory");
			e.HasKey(x => x.InventoryID);
			e.HasIndex(x => new { x.ListingID, x.BranchID }).IsUnique();
			e.Property(x => x.OnHand).HasDefaultValue(0);
			e.Property(x => x.Reserved).HasDefaultValue(0);
			e.Property(x => x.RowVersion).IsRowVersion();
			e.Property(x => x.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

			e.HasOne(x => x.Listing)
				.WithMany()
				.HasForeignKey(x => x.ListingID)
				.OnDelete(DeleteBehavior.NoAction);

			e.HasOne(x => x.Branch)
				.WithMany()
				.HasForeignKey(x => x.BranchID)
				.OnDelete(DeleteBehavior.NoAction);
		});

		modelBuilder.Entity<Branch>(e =>
		{
			e.ToTable("Branches");
			e.HasKey(b => b.BranchID);
			e.Property(b => b.IsActive).HasDefaultValue(true);
			e.Property(b => b.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
			e.Property(b => b.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
		});

		OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
