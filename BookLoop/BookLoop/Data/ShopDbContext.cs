using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

namespace BookLoop.Data;

public partial class ShopDbContext : DbContext
{
	public ShopDbContext(DbContextOptions<ShopDbContext> options)
		: base(options)
	{
	}
	public virtual DbSet<ShoppingCart> ShoppingCarts { get; set; }

	public virtual DbSet<ShoppingCartItems> ShoppingCartItems { get; set; }
	public virtual DbSet<Book> Books { get; set; }
	public virtual DbSet<BorrowRecord> BorrowRecords { get; set; }

	public virtual DbSet<Category> Categories { get; set; }

	public virtual DbSet<Listing> Listings { get; set; }

	public virtual DbSet<Order> Orders { get; set; }

	public virtual DbSet<OrderDetail> OrderDetails { get; set; }
	public DbSet<Member> Members { get; set; } = null!;


	//public DbSet<ShoppingCart> ShoppingCarts { get; set; }   // <--- 
	//public DbSet<ShoppingCartItems> ShoppingCartItems { get; set; } // <--- 

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{

		modelBuilder.Entity<Book>(entity =>
		{
			entity.HasKey(e => e.BookID).HasName("PK__Books__3DE0C227772A1333");

			entity.HasIndex(e => e.ISBN, "UQ__Books__447D36EA6E0CDF93").IsUnique();

			entity.HasIndex(e => e.Slug, "UQ__Books__BC7B5FB6B3B1BB6B").IsUnique();

			entity.Property(e => e.Description).HasMaxLength(2000);
			entity.Property(e => e.ISBN)
				.HasMaxLength(13)
				.IsUnicode(false);
			//entity.Property(e => e.LanguageCode).HasMaxLength(10).IsUnicode(false);
			entity.Property(e => e.ListPrice).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.SalePrice).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.Slug).HasMaxLength(200);
			//entity.Property(e => e.Subtitle).HasMaxLength(200);
			entity.Property(e => e.Title).HasMaxLength(100);
		});

		modelBuilder.Entity<BorrowRecord>(entity =>
		{
			entity.HasKey(e => e.RecordID).HasName("PK__BorrowRe__FBDF78C99F1B98E7");

			entity.Property(e => e.StatusCode).HasDefaultValue((byte)1);

			entity.HasOne(d => d.Listing).WithMany(p => p.BorrowRecords)
				.HasForeignKey(d => d.ListingID)
				.OnDelete(DeleteBehavior.ClientSetNull)
				.HasConstraintName("FK_BorrowRecords_Listing");
		});

		modelBuilder.Entity<Category>(entity =>
		{
			entity.HasIndex(e => e.Slug, "UQ_Categories_Slug").IsUnique();

			entity.HasIndex(e => new { e.ParentID, e.CategoryName }, "UX_Categories_ParentID_CategoryName").IsUnique();

			entity.Property(e => e.CategoryName).HasMaxLength(100);
			entity.Property(e => e.Code).HasMaxLength(50);
			entity.Property(e => e.Slug).HasMaxLength(200);

			entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
				.HasForeignKey(d => d.ParentID)
				.HasConstraintName("FK_Categories_Parent");
		});


		modelBuilder.Entity<Listing>(entity =>
		{
			entity.HasIndex(e => e.CategoryID, "IX_Listings_CategoryID");

			entity.HasIndex(e => e.ISBN, "IX_Listings_ISBN");

			entity.Property(e => e.Condition).HasMaxLength(200);
			entity.Property(e => e.ISBN)
				.HasMaxLength(13)
				.IsUnicode(false);
			entity.Property(e => e.IsAvailable).HasDefaultValue(true);
			entity.Property(e => e.Title).HasMaxLength(100);

			entity.HasOne(d => d.Category).WithMany(p => p.Listings)
				.HasForeignKey(d => d.CategoryID)
				.OnDelete(DeleteBehavior.ClientSetNull)
				.HasConstraintName("FK_Listings_Category");
		});

		modelBuilder.Entity<Order>(entity =>
		{
			entity.HasKey(e => e.OrderID).HasName("PK__Orders__C3905BAF4E2062F1");

			entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.CouponNameSnap).HasMaxLength(100);
			entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.DiscountCode).HasMaxLength(50);
			entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");
		});

		modelBuilder.Entity<OrderDetail>(entity =>
		{
			entity.HasKey(e => e.OrderDetailID).HasName("PK__OrderDet__D3B9D30C06A1A1CC");

			entity.ToTable("OrderDetail");

			entity.Property(e => e.ProductDiscountAmount).HasColumnType("decimal(10, 2)");
			entity.Property(e => e.ProductName).HasMaxLength(100);
			entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

			entity.HasOne(d => d.Book).WithMany(p => p.OrderDetails)
				.HasForeignKey(d => d.BookID)
				.OnDelete(DeleteBehavior.ClientSetNull)
				.HasConstraintName("FK_OrderDetail_Book");

			entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
				.HasForeignKey(d => d.OrderID)
				.OnDelete(DeleteBehavior.ClientSetNull)
				.HasConstraintName("FK_OrderDetail_Order");
		});

		// ShoppingCart mapping
		modelBuilder.Entity<ShoppingCart>(entity =>
		{
			entity.ToTable("ShoppingCart");
			entity.HasKey(e => e.CartID);
			entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
			entity.Property(e => e.UpdatedDate).HasDefaultValueSql("GETDATE()");
			entity.Property(e => e.IsActive).HasDefaultValue(true);

			entity.HasMany(e => e.Items)
				  .WithOne(i => i.Cart)
				  .HasForeignKey(i => i.CartID)
				  .OnDelete(DeleteBehavior.Cascade);
		});

		// ShoppingCartItems mapping
		modelBuilder.Entity<ShoppingCartItems>(entity =>
		{
			entity.ToTable("ShoppingCartItems");
			entity.HasKey(e => e.ItemID);
			entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");

			entity.HasOne(i => i.Book)
				  .WithMany()
				  .HasForeignKey(i => i.BookID)
				  .OnDelete(DeleteBehavior.Restrict);
		});




		OnModelCreatingPartial(modelBuilder);
	}

	partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
