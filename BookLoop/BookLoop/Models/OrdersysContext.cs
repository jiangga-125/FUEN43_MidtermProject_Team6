using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Ordersys.Models;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64B8EBB2EF37");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.MemberId).HasColumnName("MemberID");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BAF90C0A523");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.CouponDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CouponNameSnap).HasMaxLength(100);
            entity.Property(e => e.CouponValueSnap).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DiscountCode).HasMaxLength(50);
            entity.Property(e => e.MemberCouponId).HasColumnName("MemberCouponID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Customers");
        });

        modelBuilder.Entity<OrderAddress>(entity =>
        {
            entity.HasKey(e => e.OrderAddressId).HasName("PK__OrderAdd__34B754C54D4CEC17");

            entity.HasIndex(e => new { e.OrderId, e.AddressType }, "UQ_Order_Address").IsUnique();

            entity.Property(e => e.OrderAddressId).HasColumnName("OrderAddressID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(100);
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderAddresses)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderAddresses_Orders");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D30C0D3B085F");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.OrderDetailId).HasColumnName("OrderDetailID");
            entity.Property(e => e.BookId).HasColumnName("BookID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ProductDiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetail_Orders");
        });

        modelBuilder.Entity<OrderManagement>(entity =>
        {
            entity.HasKey(e => e.OrderMgmtId).HasName("PK__OrderMan__1F0B9535D9BD6B5F");

            entity.ToTable("OrderManagement");

            entity.Property(e => e.OrderMgmtId).HasColumnName("OrderMgmtID");
            entity.Property(e => e.LastAction).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.OrderId).HasColumnName("OrderID");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderManagements)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderManagement_Orders");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.StatusHistoryId).HasName("PK__OrderSta__DB9734B1DB1409E5");

            entity.ToTable("OrderStatusHistory");

            entity.Property(e => e.StatusHistoryId).HasColumnName("StatusHistoryID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderStatusHistory_Orders");
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.HasKey(e => e.RentalId).HasName("PK__Rentals__97005963E772CFF9");

            entity.Property(e => e.RentalId).HasColumnName("RentalID");
            entity.Property(e => e.ItemName).HasMaxLength(100);
            //entity.Property(e => e.OrderId).HasColumnName("OrderID");

            //entity.HasOne(d => d.Order).WithMany(p => p.Rentals)
                //.HasForeignKey(d => d.OrderId)
                //.OnDelete(DeleteBehavior.ClientSetNull)
                //.HasConstraintName("FK_Rentals_Orders");
        });

        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasKey(e => e.ReturnId).HasName("PK__Returns__F445E988B7DCFE93");

            entity.Property(e => e.ReturnId).HasColumnName("ReturnID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ReturnReason).HasMaxLength(200);

            entity.HasOne(d => d.Order).WithMany(p => p.Returns)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Orders");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.ShipmentId).HasName("PK__Shipment__5CAD378D2AC390A5");

            entity.Property(e => e.ShipmentId).HasColumnName("ShipmentID");
            entity.Property(e => e.AddressId).HasColumnName("AddressID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Provider).HasMaxLength(50);
            entity.Property(e => e.TrackingNumber).HasMaxLength(50);

            entity.HasOne(d => d.Address).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.AddressId)
                .HasConstraintName("FK_Shipments_OrderAddresses");

            entity.HasOne(d => d.Order).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shipments_Orders");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
