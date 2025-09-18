using System;
using System.Collections.Generic;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Data;

public partial class BookSystemContext : DbContext
{
    public BookSystemContext(DbContextOptions<BookSystemContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Author> Authors { get; set; }

    public virtual DbSet<Book> Books { get; set; }

    public virtual DbSet<BookAuthor> BookAuthors { get; set; }

    public virtual DbSet<BookImage> BookImages { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<RawBook> RawBooks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.AuthorID).HasName("PK__Authors__70DAFC147A159AB8");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.BookID).HasName("PK__Books__3DE0C227450E1A33");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Author).WithMany(p => p.Books).HasConstraintName("FK_Books_Authors");

            entity.HasOne(d => d.Category).WithMany(p => p.Books).HasConstraintName("FK_Books_Categories");

            entity.HasOne(d => d.Publisher).WithMany(p => p.Books).HasConstraintName("FK_Books_Publishers");
        });

        modelBuilder.Entity<BookAuthor>(entity =>
        {
            entity.HasOne(d => d.Author).WithMany(p => p.BookAuthors)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BookAuthors_Authors");

            entity.HasOne(d => d.Book).WithMany(p => p.BookAuthors).HasConstraintName("FK_BookAuthors_Books");
        });

        modelBuilder.Entity<BookImage>(entity =>
        {
            entity.HasKey(e => e.ImageID).HasName("PK__BookImag__7516F4ECCB42251D");

            entity.HasIndex(e => e.BookID, "UQ_BookImages_Primary")
                .IsUnique()
                .HasFilter("([IsPrimary]=(1))");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

			entity.HasOne(d => d.Book)
					  .WithMany(p => p.BookImages)   // 一對多
					  .HasForeignKey(d => d.BookID)  // TO外鍵
					  .HasConstraintName("FK_BookImages_Books");
		});

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryID).HasName("PK__Categori__19093A2B666EC644");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.PublisherID).HasName("PK__Publishe__4C657E4BA4F07C81");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<RawBook>(entity =>
        {
            entity.HasKey(e => e.RawID).HasName("PK__RawBooks__3935AC0BADCFE378");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
