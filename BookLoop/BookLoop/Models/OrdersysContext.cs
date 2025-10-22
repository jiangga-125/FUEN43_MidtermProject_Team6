using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

public partial class OrdersysContext : DbContext
{
    public OrdersysContext(DbContextOptions<OrdersysContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderAddress> OrderAddresses { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<OrderManagement> OrderManagements { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<Rental> Rentals { get; set; }

    public virtual DbSet<Return> Returns { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

	public virtual DbSet<Member> Members { get; set; }  // ← 新增

	public DbSet<Book> Books { get; set; } = null!;// ← 新增

	protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerID).HasName("PK__Customer__A4AE64B8EBB2EF37");

            entity.Property(e => e.CustomerID).HasColumnName("CustomerID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.MemberID).HasColumnName("MemberID");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderID).HasName("PK__Orders__C3905BAF90C0A523");

            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CouponNameSnap).HasMaxLength(100);
            entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CustomerID).HasColumnName("CustomerID");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DiscountCode).HasMaxLength(50);
            entity.Property(e => e.MemberCouponID).HasColumnName("MemberCouponID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Customers");
        });

        modelBuilder.Entity<OrderAddress>(entity =>
        {
            entity.HasKey(e => e.OrderAddressID).HasName("PK__OrderAdd__34B754C54D4CEC17");

            entity.HasIndex(e => new { e.OrderID, e.AddressType }, "UQ_Order_Address").IsUnique();

            entity.Property(e => e.OrderAddressID).HasColumnName("OrderAddressID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(100);
            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderAddresses)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderAddresses_Orders");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailID).HasName("PK__OrderDet__D3B9D30C0D3B085F");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.OrderDetailID).HasColumnName("OrderDetailID");
            entity.Property(e => e.BookID).HasColumnName("BookID");
            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.ProductDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetail_Orders");
        });

        modelBuilder.Entity<OrderManagement>(entity =>
        {
            entity.HasKey(e => e.OrderManagementID).HasName("PK__OrderMan__1F0B9535D9BD6B5F");

            entity.ToTable("OrderManagement");

            entity.Property(e => e.OrderManagementID).HasColumnName("OrderMgmtID");
            entity.Property(e => e.LastAction).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.OrderID).HasColumnName("OrderID");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderManagements)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderManagement_Orders");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.OrderStatusHistoryID).HasName("PK__OrderSta__DB9734B1DB1409E5");

            entity.ToTable("OrderStatusHistory");

            entity.Property(e => e.OrderStatusHistoryID).HasColumnName("StatusHistoryID");
            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderStatusHistory_Orders");
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.HasKey(e => e.RentalID).HasName("PK__Rentals__97005963E772CFF9");

            entity.Property(e => e.RentalID).HasColumnName("RentalID");
            entity.Property(e => e.ItemName).HasMaxLength(100);
            //entity.Property(e => e.OrderID).HasColumnName("OrderID");

            //entity.HasOne(d => d.Order).WithMany(p => p.Rentals)
                //.HasForeignKey(d => d.OrderID)
                //.OnDelete(DeleteBehavior.ClientSetNull)
                //.HasConstraintName("FK_Rentals_Orders");
        });

        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasKey(e => e.ReturnID).HasName("PK__Returns__F445E988B7DCFE93");

            entity.Property(e => e.ReturnID).HasColumnName("ReturnID");
            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.ReturnReason).HasMaxLength(200);

            entity.HasOne(d => d.Order).WithMany(p => p.Returns)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Orders");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.ShipmentID).HasName("PK__Shipment__5CAD378D2AC390A5");

            entity.Property(e => e.ShipmentID).HasColumnName("ShipmentID");
            entity.Property(e => e.AddressID).HasColumnName("AddressID");
            entity.Property(e => e.OrderID).HasColumnName("OrderID");
            entity.Property(e => e.Provider).HasMaxLength(50);
            entity.Property(e => e.TrackingNumber).HasMaxLength(50);

            entity.HasOne(d => d.Address).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.AddressID)
                .HasConstraintName("FK_Shipments_OrderAddresses");

            entity.HasOne(d => d.Order).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shipments_Orders");
        });

		modelBuilder.Entity<Member>(entity =>
		{
			entity.HasKey(e => e.MemberID);
			entity.Property(e => e.MemberID).HasColumnName("MemberID");
			entity.Property(e => e.Username).HasMaxLength(100);
			// 其他欄位設定
		});

		OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
