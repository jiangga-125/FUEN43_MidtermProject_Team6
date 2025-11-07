using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using MemberTokenEntity = global::BookLoop.Models.MemberToken;


namespace BookLoop.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

		// ===== ��x�]�즳�^ =====
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
		public DbSet<MailTemplate> MailTemplates { get; set; } = null!;
		public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;   // ��x Users �� refresh

		// ===== �e�x�]�s�W/�j�ơ^ =====
		public DbSet<Member> Members => Set<Member>();
		public DbSet<MemberLogin> MemberLogins => Set<MemberLogin>();
		public DbSet<MemberTokenEntity> MemberTokens => Set<MemberTokenEntity>();
		public DbSet<MemberTrustedDevice> MemberTrustedDevices => Set<MemberTrustedDevice>();
		public DbSet<MemberRecoveryCode> MemberRecoveryCodes => Set<MemberRecoveryCode>();
		public DbSet<MemberRefreshToken> MemberRefreshTokens => Set<MemberRefreshToken>(); // �e�x Members �� refresh

		//public DbSet<MailTemplate> MailTemplates { get; set; }
		public DbSet<Template> Templates => Set<Template>();
		public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
        public DbSet<MailSendLog> MailSendLogs => Set<MailSendLog>();
        public DbSet<MailJob> MailJobs => Set<MailJob>();
        public DbSet<MailJobRecipient> MailJobRecipients => Set<MailJobRecipient>();
		public DbSet<MailEvent> MailEvents { get; set; } = null!;
		public DbSet<IntegrationCursor> IntegrationCursors { get; set; } = null!;


		//public DbSet<RefreshToken> RefreshTokens { get; set; } = null!; // ?��?JWT RefreshTokens


		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// �Y�A����L EF �]�w�ɡA�O�d�F���䤤�Y�� DisplayName/Ip/UserAgent/Purpose/Code ���]�w��������
			// ===== 1) ?��??��??��??�本 DbContext �????DbSet<> =====
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

			// ===== ��x =====
			modelBuilder.Entity<User>(e =>
			{
				e.ToTable("USERS");
				e.HasKey(x => x.UserID);
				e.Property(x => x.Email).IsRequired().HasMaxLength(254);
				e.HasIndex(x => x.Email).IsUnique(false);
				// �� ���n�]�w e.Property(x => x.DisplayName)�]�ҫ��S���^
			});

			modelBuilder.Entity<Role>(e =>
			{
				e.ToTable("ROLES");
				e.HasKey(x => x.RoleID);
				e.HasIndex(x => x.RoleCode).IsUnique();
			});

			modelBuilder.Entity<Permission>(e =>
			{
				e.ToTable("PERMISSIONS");
				e.HasKey(x => x.PermissionID);
				e.HasIndex(x => x.PermKey).IsUnique();
			});

			modelBuilder.Entity<Supplier>(e =>
			{
				e.ToTable("SUPPLIERS");
				e.HasKey(x => x.SupplierID);
				e.HasIndex(x => x.SupplierCode).IsUnique();
			});

			modelBuilder.Entity<Feature>(e =>
			{
				e.ToTable("FEATURES");
				e.HasKey(x => x.FeatureID);
				e.HasIndex(x => x.Code).IsUnique();
			});

			// ===== �e�x =====
			modelBuilder.Entity<Member>(e =>
			{
				e.ToTable("Members");
				e.HasKey(x => x.MemberID);

				e.Property(x => x.RowVersion).IsRowVersion();

				e.HasIndex(x => x.Username).IsUnique();
				e.HasIndex(x => x.EmailNormalized)
					.IsUnique()
					.HasFilter("[EmailNormalized] IS NOT NULL");
			});

			modelBuilder.Entity<MemberLogin>(e =>
			{
				e.ToTable("MemberLogins");
				e.HasKey(x => x.MemberLoginID);
				e.HasOne(x => x.Member)
					.WithMany(m => m.Logins)
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();
			});

			modelBuilder.Entity<MemberTokenEntity>(e =>
			{
				e.ToTable("MemberTokens");
				e.HasKey(x => x.MemberTokenID);
				e.HasOne(x => x.Member)
					.WithMany()
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				// 對齊欄位型別/長度
				e.Property(x => x.Token)
					.HasMaxLength(16)         // nvarchar(16)
					.IsUnicode(true);

				// 明確對齊欄位名稱（以免未來屬性名改動）
				e.Property(x => x.ExpiresAtUtc).HasColumnName("ExpiresAtUtc");
				e.Property(x => x.ConsumedAtUtc).HasColumnName("ConsumedAtUtc");
				e.Property(x => x.CreatedAt).HasColumnName("CreatedAt");

				// 查詢常用索引
				e.HasIndex(x => new { x.MemberID, x.TokenType });
				// ★ 只保留安全索引，不要設定 Purpose/Ip/UserAgent
				//e.HasIndex(x => new { x.MemberID, x.TokenType });
				//e.HasIndex(x => x.Token).IsUnique();
			});

			modelBuilder.Entity<MemberTrustedDevice>(e =>
			{
				e.ToTable("MemberTrustedDevices");
				e.HasKey(x => x.TrustedDeviceID);
				e.HasOne(x => x.Member)
					.WithMany()
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				// �� ���n�]�w Ip�F�u���ߤ@����
				e.HasIndex(x => new { x.MemberID, x.DeviceHash }).IsUnique();
			});

			modelBuilder.Entity<MemberRecoveryCode>(e =>
			{
				e.ToTable("MemberRecoveryCodes");
				e.HasKey(x => x.MemberRecoveryCodeID);
				e.HasOne(x => x.Member)
					.WithMany()
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				// �� ���n�]�w Code/Purpose�F�u�w�� MemberID �د���
				e.HasIndex(x => x.MemberID);
			});

			modelBuilder.Entity<MemberRefreshToken>(e =>
			{
				e.ToTable("MemberRefreshTokens");
				e.HasKey(x => x.Id);
				e.HasIndex(x => x.MemberId);
				e.HasIndex(x => x.TokenHash).IsUnique();
				// �� ���n�]�w Ip/UserAgent
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

            modelBuilder.Entity<TemplateVersion>() // 篩選?��?：�???Template ?�能 1 ?��?設�?
				.HasIndex(v => new { v.TemplateId, v.IsDefault })
				.HasFilter("[IsDefault] = 1")
				.IsUnique();

			//b.Entity<PermissionFeature>(e =>
			//{
			//	e.ToTable("PERMISSION_FEATURES"); // ????DB 一??
			//	e.HasKey(x => new { x.PermissionID, x.FeatureID });
			//	e.HasOne(x => x.Permission).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.PermissionID);
			//	e.HasOne(x => x.Feature).WithMany(x => x.PermissionFeatures).HasForeignKey(x => x.FeatureID);
			//});


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
				e.HasIndex(x => x.SendAt);        //預定時間
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
