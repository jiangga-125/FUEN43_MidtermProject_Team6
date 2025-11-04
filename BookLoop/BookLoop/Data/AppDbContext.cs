using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
		//public DbSet<MailTemplate> MailTemplates { get; set; }
		public DbSet<Template> Templates => Set<Template>();
		public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
        public DbSet<MailSendLog> MailSendLogs => Set<MailSendLog>();
        public DbSet<MailJob> MailJobs => Set<MailJob>();
        public DbSet<MailJobRecipient> MailJobRecipients => Set<MailJobRecipient>();
		public DbSet<MailEvent> MailEvents { get; set; } = null!;
		public DbSet<IntegrationCursor> IntegrationCursors { get; set; } = null!;



		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// ===== 1) 白名單：只保留本 DbContext 宣告的 DbSet<> =====
			//var allowedTypes = this.GetType()
			//	.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			//	.Where(p => p.PropertyType.IsGenericType &&
			//				p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
			//	.Select(p => p.PropertyType.GetGenericArguments()[0])
			//	.ToHashSet();

			//var toIgnore = b.Model.GetEntityTypes()
			//	.Where(et => et.ClrType != null && !allowedTypes.Contains(et.ClrType))
			//	.ToList();

			//foreach (var et in toIgnore)
			//	b.Ignore(et.ClrType!);

			modelBuilder.ApplyConfigurationsFromAssembly(typeof(PermissionFeatureConfiguration).Assembly);

			// USERS
			modelBuilder.Entity<User>(e =>
			{
				e.ToTable("USERS");
				e.HasKey(x => x.UserID);
				e.Property(x => x.Email).IsRequired().HasMaxLength(254);
				e.HasIndex(x => x.Email).IsUnique(false);
			});

			// ROLES
			modelBuilder.Entity<Role>(e =>
			{
				e.ToTable("ROLES");
				e.HasKey(x => x.RoleID);
				e.HasIndex(x => x.RoleCode).IsUnique();
			});

			// USER_ROLES
			//b.Entity<UserRole>(e =>
			//{
			//	e.ToTable("USER_ROLES");
			//	e.HasKey(x => new { x.UserID, x.RoleID });
			//	e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserID);
			//	e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleID);
			//});

			// PERMISSIONS
			modelBuilder.Entity<Permission>(e =>
			{
				e.ToTable("PERMISSIONS");
				e.HasKey(x => x.PermissionID);
				e.HasIndex(x => x.PermKey).IsUnique();
			});

			// USER_PERMISSIONS
			//b.Entity<UserPermission>(e =>
			//{
			//	e.ToTable("USER_PERMISSIONS");
			//	e.HasKey(x => new { x.UserID, x.PermissionID });
			//	e.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserID);
			//	e.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionID);
			//});

			// SUPPLIERS
			modelBuilder.Entity<Supplier>(e =>
			{
				e.ToTable("SUPPLIERS");
				e.HasKey(x => x.SupplierID);
				e.HasIndex(x => x.SupplierCode).IsUnique();
			});

			// SUPPLIER_USERS
			//b.Entity<SupplierUser>(e =>
			//{
			//	e.ToTable("SUPPLIER_USERS");
			//	e.HasKey(x => new { x.SupplierID, x.UserID });
			//	e.HasOne(x => x.Supplier).WithMany(x => x.SupplierUsers).HasForeignKey(x => x.SupplierID);
			//	e.HasOne(x => x.User).WithMany(x => x.SupplierUsers).HasForeignKey(x => x.UserID);
			//});

			// FEATURES
			modelBuilder.Entity<Feature>(e =>
			{
				e.ToTable("FEATURES");
				e.HasKey(x => x.FeatureID);
				e.HasIndex(x => x.Code).IsUnique();
			});

            //Mail
            modelBuilder.Entity<Template>().ToTable("Template");
            modelBuilder.Entity<TemplateVersion>().ToTable("TemplateVersion");

            modelBuilder.Entity<Template>().HasIndex(x => x.TemplateKey).IsUnique();

            modelBuilder.Entity<TemplateVersion>()
				.HasOne(v => v.Template).WithMany(t => t.Versions)
				.HasForeignKey(v => v.TemplateId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TemplateVersion>()
				.HasIndex(v => new { v.TemplateId, v.TemplateName }).IsUnique();

            modelBuilder.Entity<TemplateVersion>() // 篩選唯一：每個 Template 只能 1 個預設版
				.HasIndex(v => new { v.TemplateId, v.IsDefault })
				.HasFilter("[IsDefault] = 1")
				.IsUnique();

            modelBuilder.Entity<MailJob>(e =>
            {
                e.ToTable("MailJob");
                e.HasKey(x => x.JobId);

                e.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
                e.Property(x => x.CampaignName).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(1000);

                // 狀態預設
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Scheduled");

                // 進度欄位預設
                e.Property(x => x.TotalRecipients).HasDefaultValue(0);
                e.Property(x => x.SentCount).HasDefaultValue(0);

                // 常用查詢索引
                e.HasIndex(x => x.SendAt);        // 預定時間
                e.HasIndex(x => x.TemplateKey);
                e.HasIndex(x => x.Status);

                // 讓排程佇列常用的條件更快（狀態＋時間）
                e.HasIndex(x => new { x.Status, x.SendAt })
                 .HasDatabaseName("IX_MailJob_Status_SendAt");
            });

            // MailJobRecipient（名單明細）— 一封信＝一筆
            modelBuilder.Entity<MailJobRecipient>(e =>
            {
                e.ToTable("MailJobRecipient");
                e.HasKey(x => x.MailJobRecipientId);

                e.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
                e.Property(x => x.RecipientName).HasMaxLength(200);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
                e.Property(x => x.Error).HasMaxLength(1000);

                e.HasIndex(x => new { x.MailJobId, x.Status });
                e.HasIndex(x => x.RecipientEmail);

                e.HasOne(x => x.MailJob)
                 .WithMany(j => j.Recipients)
                 .HasForeignKey(x => x.MailJobId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // MailSendLog（日誌）— 讓日誌 1:1 對到名單明細
            modelBuilder.Entity<MailSendLog>(e =>
            {
                e.ToTable("MailSendLog");

                // 索引到 JobRecipientId，細看單一收件者的日誌會更快
                e.HasIndex(x => x.JobRecipientId);

                // 若你要加強關聯（等舊資料處理完再開啟）
                // e.HasOne<MailJobRecipient>()
                //   .WithMany()
                //   .HasForeignKey(x => x.JobRecipientId)
                //   .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
