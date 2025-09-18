using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace BookLoop.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

		// �u�C�X�A�n�ѳo�� Context �޲z������
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

			// ===== 1) �H�u�� DbContext �ŧi�� DbSet<>�v�إߥզW�� =====
			var allowedTypes = this.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.PropertyType.IsGenericType &&
							p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
				.Select(p => p.PropertyType.GetGenericArguments()[0])
				.ToHashSet();

			// �ثe�Q�ҫ����������O�]�i��]�t�~���M��/�P�ƪ�����^
			var toIgnore = b.Model.GetEntityTypes()
				.Where(et => et.ClrType != null && !allowedTypes.Contains(et.ClrType))
				.ToList();

			// �@�ߩ����u���b�զW��v������]�קK�ʥD�䵥���D�^
			foreach (var et in toIgnore)
			{
				b.Ignore(et.ClrType!);
			}

			// ===== 2) �A���쥻 Fluent �]�w�]�u�]�w�զW�椺������^ =====

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

			// USER_ROLES (many-to-many)
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

			// USER_PERMISSIONS (many-to-many)
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

			// PERMISSION_FEATURES (many-to-many)
			b.Entity<PermissionFeature>(e =>
			{
				e.ToTable("PermissionFeatures");
				e.HasKey(x => new { x.PermissionID, x.FeatureID });
				e.HasOne(x => x.Permission).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.PermissionID);
				e.HasOne(x => x.Feature).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.FeatureID);
			});
		}
	}
}
