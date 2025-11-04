using BookLoop.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

		// ===== 後台（原有） =====
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
		public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;   // 後台 Users 用 refresh

		// ===== 前台（新增/強化） =====
		public DbSet<Member> Members => Set<Member>();
		public DbSet<MemberLogin> MemberLogins => Set<MemberLogin>();
		public DbSet<MemberToken> MemberTokens => Set<MemberToken>();
		public DbSet<MemberTrustedDevice> MemberTrustedDevices => Set<MemberTrustedDevice>();
		public DbSet<MemberRecoveryCode> MemberRecoveryCodes => Set<MemberRecoveryCode>();
		public DbSet<MemberRefreshToken> MemberRefreshTokens => Set<MemberRefreshToken>(); // 前台 Members 用 refresh

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// 若你有其他 EF 設定檔，保留；但其中若有 DisplayName/Ip/UserAgent/Purpose/Code 的設定必須移除
			modelBuilder.ApplyConfigurationsFromAssembly(typeof(PermissionFeatureConfiguration).Assembly);

			// ===== 後台 =====
			modelBuilder.Entity<User>(e =>
			{
				e.ToTable("USERS");
				e.HasKey(x => x.UserID);
				e.Property(x => x.Email).IsRequired().HasMaxLength(254);
				e.HasIndex(x => x.Email).IsUnique(false);
				// ★ 不要設定 e.Property(x => x.DisplayName)（模型沒有）
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

			// ===== 前台 =====
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

				// ★ 只保留安全索引，不要設定 Purpose/Ip/UserAgent
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

				// ★ 不要設定 Ip；只做唯一索引
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

				// ★ 不要設定 Code/Purpose；只針對 MemberID 建索引
				e.HasIndex(x => x.MemberID);
			});

			modelBuilder.Entity<MemberRefreshToken>(e =>
			{
				e.ToTable("MemberRefreshTokens");
				e.HasKey(x => x.Id);
				e.HasIndex(x => x.MemberId);
				e.HasIndex(x => x.TokenHash).IsUnique();
				// ★ 不要設定 Ip/UserAgent
			});
		}
	}
}
