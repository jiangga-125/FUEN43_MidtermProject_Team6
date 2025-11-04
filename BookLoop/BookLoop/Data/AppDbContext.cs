using BookLoop.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

		// ===== «á¥x¡]­ì¦³¡^ =====
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
		public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;   // «á¥x Users ¥Î refresh

		// ===== «e¥x¡]·s¼W/±j¤Æ¡^ =====
		public DbSet<Member> Members => Set<Member>();
		public DbSet<MemberLogin> MemberLogins => Set<MemberLogin>();
		public DbSet<MemberToken> MemberTokens => Set<MemberToken>();
		public DbSet<MemberTrustedDevice> MemberTrustedDevices => Set<MemberTrustedDevice>();
		public DbSet<MemberRecoveryCode> MemberRecoveryCodes => Set<MemberRecoveryCode>();
		public DbSet<MemberRefreshToken> MemberRefreshTokens => Set<MemberRefreshToken>(); // «e¥x Members ¥Î refresh

		//public DbSet<MailTemplate> MailTemplates { get; set; }
		public DbSet<Template> Templates => Set<Template>();
		public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
        public DbSet<MailSendLog> MailSendLogs => Set<MailSendLog>();
        public DbSet<MailJob> MailJobs => Set<MailJob>();
        public DbSet<MailJobRecipient> MailJobRecipients => Set<MailJobRecipient>();


		//public DbSet<RefreshToken> RefreshTokens { get; set; } = null!; // ?°å?JWT RefreshTokens


		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// ­Y§A¦³¨ä¥L EF ³]©wÀÉ¡A«O¯d¡F¦ý¨ä¤¤­Y¦³ DisplayName/Ip/UserAgent/Purpose/Code ªº³]©w¥²¶·²¾°£
			// ===== 1) ?½å??®ï??ªä??™æœ¬ DbContext å®????DbSet<> =====
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

			// ===== «á¥x =====
			modelBuilder.Entity<User>(e =>
			{
				e.ToTable("USERS");
				e.HasKey(x => x.UserID);
				e.Property(x => x.Email).IsRequired().HasMaxLength(254);
				e.HasIndex(x => x.Email).IsUnique(false);
				// ¡¹ ¤£­n³]©w e.Property(x => x.DisplayName)¡]¼Ò«¬¨S¦³¡^
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

			// ===== «e¥x =====
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

			modelBuilder.Entity<MemberToken>(e =>
			{
				e.ToTable("MemberTokens");
				e.HasKey(x => x.MemberTokenID);
				e.HasOne(x => x.Member)
					.WithMany()
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				// ¡¹ ¥u«O¯d¦w¥þ¯Á¤Þ¡A¤£­n³]©w Purpose/Ip/UserAgent
				e.HasIndex(x => new { x.MemberID, x.TokenType });
				e.HasIndex(x => x.Token).IsUnique();
			});

			modelBuilder.Entity<MemberTrustedDevice>(e =>
			{
				e.ToTable("MemberTrustedDevices");
				e.HasKey(x => x.TrustedDeviceID);
				e.HasOne(x => x.Member)
					.WithMany()
					.HasForeignKey(x => x.MemberID)
					.OnDelete(DeleteBehavior.Cascade);

				// ¡¹ ¤£­n³]©w Ip¡F¥u°µ°ß¤@¯Á¤Þ
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

				// ¡¹ ¤£­n³]©w Code/Purpose¡F¥u°w¹ï MemberID «Ø¯Á¤Þ
				e.HasIndex(x => x.MemberID);
			});

			modelBuilder.Entity<MemberRefreshToken>(e =>
			{
				e.ToTable("MemberRefreshTokens");
				e.HasKey(x => x.Id);
				e.HasIndex(x => x.MemberId);
				e.HasIndex(x => x.TokenHash).IsUnique();
				// ¡¹ ¤£­n³]©w Ip/UserAgent
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

            modelBuilder.Entity<TemplateVersion>() // ç¯©é¸?¯ä?ï¼šæ???Template ?ªèƒ½ 1 ?‹é?è¨­ç?
				.HasIndex(v => new { v.TemplateId, v.IsDefault })
				.HasFilter("[IsDefault] = 1")
				.IsUnique();

			//b.Entity<PermissionFeature>(e =>
			//{
			//	e.ToTable("PERMISSION_FEATURES"); // ????DB ä¸€??
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

                // ï¿½ï¿½ï¿½Aï¿½wï¿½]
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Scheduled");

                // ï¿½iï¿½ï¿½ï¿½ï¿½ï¿½wï¿½]
                e.Property(x => x.TotalRecipients).HasDefaultValue(0);
                e.Property(x => x.SentCount).HasDefaultValue(0);

                // ï¿½`ï¿½Î¬dï¿½ß¯ï¿½ï¿½ï¿½
                e.HasIndex(x => x.SendAt);        // ï¿½wï¿½wï¿½É¶ï¿½
                e.HasIndex(x => x.TemplateKey);
                e.HasIndex(x => x.Status);

                // ï¿½ï¿½ï¿½Æµ{ï¿½ï¿½Cï¿½`ï¿½Îªï¿½ï¿½ï¿½ï¿½ï¿½ï¿½Ö¡]ï¿½ï¿½ï¿½Aï¿½Ï®É¶ï¿½ï¿½^
                e.HasIndex(x => new { x.Status, x.SendAt })
                 .HasDatabaseName("IX_MailJob_Status_SendAt");
            });

            // MailJobRecipientï¿½]ï¿½Wï¿½ï¿½ï¿½ï¿½Ó¡^ï¿½X ï¿½@ï¿½Ê«Hï¿½×¤@ï¿½ï¿½
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

            // MailSendLogï¿½]ï¿½ï¿½xï¿½^ï¿½X ï¿½ï¿½ï¿½ï¿½x 1:1 ï¿½ï¿½ï¿½Wï¿½ï¿½ï¿½ï¿½ï¿?
            modelBuilder.Entity<MailSendLog>(e =>
            {
                e.ToTable("MailSendLog");

                // ï¿½ï¿½ï¿½Þ¨ï¿½ JobRecipientIdï¿½Aï¿½Ó¬Ý³ï¿½@ï¿½ï¿½ï¿½ï¿½Ìªï¿½ï¿½ï¿½xï¿½|ï¿½ï¿½ï¿?
                e.HasIndex(x => x.JobRecipientId);

                // ï¿½Yï¿½Aï¿½nï¿½[ï¿½jï¿½ï¿½ï¿½pï¿½]ï¿½ï¿½ï¿½Â¸ï¿½Æ³Bï¿½zï¿½ï¿½ï¿½Aï¿½}ï¿½Ò¡^
                // e.HasOne<MailJobRecipient>()
                //   .WithMany()
                //   .HasForeignKey(x => x.JobRecipientId)
                //   .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
