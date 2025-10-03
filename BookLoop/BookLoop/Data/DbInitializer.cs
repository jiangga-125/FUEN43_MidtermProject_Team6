using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Data
{
    public class DbInitializer
    {
        private readonly AppDbContext _db;
        public DbInitializer(AppDbContext db) => _db = db;

        public async Task EnsureAdminPasswordAsync(string adminEmail, string initPassword)
        {
            var admin = await _db.Users.FirstOrDefaultAsync(x => x.Email == adminEmail);
            if (admin == null)
            {
                admin = new User
                {
                    Email = adminEmail,
                    UserType = 2,
                    Status = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    MustChangePassword = false,
                    PasswordHash = initPassword // 簡化：純文字；你的 AuthService 會相容
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
            // 基本 Permission：只建立 ADMIN/VENDOR/SALES 三筆；不建立 Accounts.* 類型
            var seeds = new (string key, string name)[]
            {
                ("ADMIN", "管理員"),
                ("VENDOR", "書商"),
                ("SALES", "銷售")
            };
            var exist = await _db.Permissions.Select(p => p.PermKey).ToListAsync();
            foreach (var (key, name) in seeds)
            {
                if (!exist.Contains(key))
                    _db.Permissions.Add(new Permission { PermKey = key, PermName = name, PermGroup = "Core" });
            }
            await _db.SaveChangesAsync();

            // 功能清單（頁面/功能）
            var featureSeeds = new (string code, string name, string group, bool isPage, int sort)[]
            {
                ("Users.Index","帳號清單","Accounts", true, 10),
                ("Users.Edit","帳號編輯","Accounts", false, 11),
                ("Permissions.Index","權限管理","Permissions", true, 20),
                ("Permissions.Assign","適用帳號設定","Permissions", false, 21),
                ("Permissions.Features","功能設定","Permissions", false, 22),
                ("Members.Index","會員清單","Members", true, 30),
            };
            var fexist = await _db.Features.Select(f => f.Code).ToListAsync();
            foreach (var f in featureSeeds)
            {
                if (!fexist.Contains(f.code))
                    _db.Features.Add(new Feature { Code = f.code, Name = f.name, FeatureGroup = f.group, IsPageLevel = f.isPage, SortOrder = f.sort });
            }
            await _db.SaveChangesAsync();

            // 讓 ADMIN 擁有所有功能
            var adminPerm = await _db.Permissions.SingleAsync(p => p.PermKey == "ADMIN");
            var allFids = await _db.Features.Select(f => f.FeatureID).ToListAsync();
            var current = await _db.PermissionFeatures.Where(pf => pf.PermissionID == adminPerm.PermissionID).Select(pf => pf.FeatureID).ToListAsync();
            var toAdd = allFids.Except(current).ToList();
            foreach (var fid in toAdd)
                _db.PermissionFeatures.Add(new PermissionFeature { PermissionID = adminPerm.PermissionID, FeatureID = fid });
            await _db.SaveChangesAsync();

            // admin 帳號擁有 ADMIN 這個 Permission
            var admin = await _db.Users.SingleAsync(u => u.Email == adminEmail);
            bool has = await _db.UserPermissions.AnyAsync(up => up.UserID == admin.UserID && up.PermissionID == adminPerm.PermissionID);
            if (!has)
            {
                _db.UserPermissions.Add(new UserPermission { UserID = admin.UserID, PermissionID = adminPerm.PermissionID });
                await _db.SaveChangesAsync();
            }
        }
    }
}
