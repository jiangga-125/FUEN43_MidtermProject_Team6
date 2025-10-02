using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace BookLoop.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

		public DbSet<User> Users => Set<User>();
		public DbSet<Role> Roles => Set<Role>();
		public DbSet<Permission> Permissions => Set<Permission>();
		public DbSet<UserRole> UserRoles => Set<UserRole>();
		public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
		public DbSet<Supplier> Suppliers => Set<Supplier>();
		public DbSet<SupplierUser> SupplierUsers => Set<SupplierUser>();
		public DbSet<Feature> Features => Set<Feature>();
		public DbSet<PermissionFeature> PermissionFeatures => Set<PermissionFeature>();
		public DbSet<Blacklist> Blacklists => Set<Blacklist>();
		public DbSet<Member> Members => Set<Member>();

		protected override void OnModelCreating(ModelBuilder b)
		{
			base.OnModelCreating(b);

			// ===== 1) 白名單：只保留本 DbContext 宣告的 DbSet<> =====
			var allowedTypes = this.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.PropertyType.IsGenericType &&
							p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
				.Select(p => p.PropertyType.GetGenericArguments()[0])
				.ToHashSet();

			var toIgnore = b.Model.GetEntityTypes()
				.Where(et => et.ClrType != null && !allowedTypes.Contains(et.ClrType))
				.ToList();

			foreach (var et in toIgnore)
				b.Ignore(et.ClrType!);

			// ===== 2) Fluent 設定 =====

			// USERS
			b.Entity<User>(e =>
			{
				e.ToTable("USERS");
				e.HasKey(x => x.UserID);
				e.Property(x => x.Email).IsRequired().HasMaxLength(254);
				e.HasIndex(x => x.Email).IsUnique(false);
			});

			// ROLES
			b.Entity<Role>(e =>
			{
				e.ToTable("ROLES");
				e.HasKey(x => x.RoleID);
				e.HasIndex(x => x.RoleCode).IsUnique();
			});

			// USER_ROLES
			b.Entity<UserRole>(e =>
			{
				e.ToTable("USER_ROLES");
				e.HasKey(x => new { x.UserID, x.RoleID });
				e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserID);
				e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleID);
			});

			// PERMISSIONS
			b.Entity<Permission>(e =>
			{
				e.ToTable("PERMISSIONS");
				e.HasKey(x => x.PermissionID);
				e.HasIndex(x => x.PermKey).IsUnique();
			});

			// USER_PERMISSIONS
			b.Entity<UserPermission>(e =>
			{
				e.ToTable("USER_PERMISSIONS");
				e.HasKey(x => new { x.UserID, x.PermissionID });
				e.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserID);
				e.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionID);
			});

			// SUPPLIERS
			b.Entity<Supplier>(e =>
			{
				e.ToTable("SUPPLIERS");
				e.HasKey(x => x.SupplierID);
				e.HasIndex(x => x.SupplierCode).IsUnique();
			});

			// SUPPLIER_USERS
			b.Entity<SupplierUser>(e =>
			{
				e.ToTable("SUPPLIER_USERS");
				e.HasKey(x => new { x.SupplierID, x.UserID });
				e.HasOne(x => x.Supplier).WithMany(x => x.SupplierUsers).HasForeignKey(x => x.SupplierID);
				e.HasOne(x => x.User).WithMany(x => x.SupplierUsers).HasForeignKey(x => x.UserID);
			});

			// FEATURES
			b.Entity<Feature>(e =>
			{
				e.ToTable("FEATURES");
				e.HasKey(x => x.FeatureID);
				e.HasIndex(x => x.Code).IsUnique();
			});

			// PERMISSION_FEATURES
			b.Entity<PermissionFeature>(e =>
			{
<<<<<<< HEAD
				e.ToTable("Permission_Features"); // 修改PermissionFeatures成Permission_Features
=======
				e.ToTable("PERMISSION_FEATURES"); // ← 與 DB 一致
>>>>>>> RMupload
				e.HasKey(x => new { x.PermissionID, x.FeatureID });
				e.HasOne(x => x.Permission).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.PermissionID);
				e.HasOne(x => x.Feature).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.FeatureID);
			});
		}
	}
}
