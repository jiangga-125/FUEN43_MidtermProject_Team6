using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Data
{
	public class DbInitializer
	{
		private readonly AppDbContext _db;
		public DbInitializer(AppDbContext db) => _db = db;

		// 建議：第一次啟動時呼叫
		public async Task EnsureAdminPasswordAsync(string adminEmail, string initPassword)
		{
			var admin = await _db.Users.FirstOrDefaultAsync(x => x.Email == adminEmail);
			if (admin == null)
			{
				admin = new User
				{
					Email = adminEmail,
					UserType = 2, // 員工
					Status = 1,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow,
					MustChangePassword = false,
					PasswordHash = initPassword // 你目前的 AuthService/流程相容
				};
				_db.Users.Add(admin);
				await _db.SaveChangesAsync();
			}
			else if (string.IsNullOrEmpty(admin.PasswordHash))
			{
				admin.PasswordHash = initPassword;
				admin.UpdatedAt = DateTime.UtcNow;
				await _db.SaveChangesAsync();
			}

			await EnsurePermissionAndFeatureSeedAsync(adminEmail);
		}

		public async Task EnsurePermissionAndFeatureSeedAsync(string adminEmail)
		{
			// 1) 基本 Permission：只建立不存在的
			var seeds = new (string key, string name)[]
			{
		("ADMIN", "管理員"),
		("VENDOR", "書商"),
		("SALES", "銷售")
			};

			foreach (var (key, name) in seeds)
			{
				var existed = await _db.Permissions.SingleOrDefaultAsync(p => p.PermKey == key);
				if (existed == null)
				{
					_db.Permissions.Add(new Permission
					{
						PermKey = key,
						PermName = name,
						PermGroup = "Core"
					});
				}
				else
				{
					// 如要同步名稱/群組，直接改屬性（不要 Update）
					existed.PermName = name;
					existed.PermGroup = "Core";
				}
			}
			await _db.SaveChangesAsync();

			// 2) 功能清單 upsert（以 Code 為準）
			var featureSeeds = new (string code, string name, string group, bool isPage, int sort)[]
			{
        // Accounts 模組
        ("Account.Access","帳戶模組","Account", true, 5),

		("Users.Index","帳號清單","Users", true, 10),
		("Users.Create","帳號新增","Users", false, 11),
		("Users.Edit","帳號編輯","Users", false, 12),
		("Users.Delete","帳號刪除","Users", false, 13),

		("Permissions.Index","權限管理","Permissions", true, 20),
		("Permissions.Assign","適用帳號設定","Permissions", false, 21),
		("Permissions.Features","功能設定","Permissions", false, 22),

		("Members.Index","會員清單","Members", true, 30),
		("Members.Create","會員新增","Members", false, 31),
		("Members.Edit","會員編輯","Members", false, 32),
		("Members.Delete","會員刪除","Members", false, 33),

		("Blacklists.Index","黑名單清單","Members", true, 34),
		("Blacklists.Create","黑名單新增","Members", false, 35),
		("Blacklists.Manage","黑名單維護","Members", false, 36),
		("Blacklists.Delete","黑名單刪除","Members", false, 37),

        // 其他模組入口（如需）
        ("Books.Access","書籍模組","Books", true, 40),
		("Orders.Access","訂單模組","Orders", true, 50),
		("Borrow.Access","二手書模組","Borrow", true, 60),

        // 報表系（保留主要入口，移除 Reports.Index）
        ("ReportMail.Access","報表系統","Reports", true, 95),
			};

			foreach (var f in featureSeeds)
			{
				var existed = await _db.Features.SingleOrDefaultAsync(x => x.Code == f.code);
				if (existed == null)
				{
					_db.Features.Add(new Feature
					{
						Code = f.code,
						Name = f.name,
						FeatureGroup = f.group,
						IsPageLevel = f.isPage,
						SortOrder = f.sort
					});
				}
				else
				{
					// 只改屬性，不呼叫 Update（避免第二個實例被追蹤）
					existed.Name = f.name;
					existed.FeatureGroup = f.group;
					existed.IsPageLevel = f.isPage;
					existed.SortOrder = f.sort;
				}
			}
			await _db.SaveChangesAsync();

			// 3) 讓 ADMIN 擁有全部功能（差異新增即可）
			var adminPerm = await _db.Permissions.SingleAsync(p => p.PermKey == "ADMIN");
			var allFids = await _db.Features.Select(f => f.FeatureID).ToListAsync();
			var current = await _db.PermissionFeatures
				.Where(pf => pf.PermissionID == adminPerm.PermissionID)
				.Select(pf => pf.FeatureID)
				.ToListAsync();

			var toAdd = allFids.Except(current);
			foreach (var fid in toAdd)
			{
				_db.PermissionFeatures.Add(new PermissionFeature
				{
					PermissionID = adminPerm.PermissionID,
					FeatureID = fid
				});
			}
			await _db.SaveChangesAsync();

			// 4) admin 帳號給 ADMIN 這個 Permission
			var admin = await _db.Users.SingleAsync(u => u.Email == adminEmail);
			bool has = await _db.UserPermissions
				.AnyAsync(up => up.UserID == admin.UserID && up.PermissionID == adminPerm.PermissionID);
			if (!has)
			{
				_db.UserPermissions.Add(new UserPermission
				{
					UserID = admin.UserID,
					PermissionID = adminPerm.PermissionID
				});
				await _db.SaveChangesAsync();
			}
		}
	}
}
