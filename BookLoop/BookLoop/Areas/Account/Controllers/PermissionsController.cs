using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Account.Controllers
{
    [Authorize(Policy = "Permissions.Manage")]
    [Area("Account")]
	public class PermissionsController : Controller
    {
        private readonly AppDbContext _db;
        public PermissionsController(AppDbContext db) => _db = db;

        // 只顯示「集合型」權限（ADMIN/VENDOR/SALES/自訂），不顯示 Accounts.*
        public async Task<IActionResult> Index(string? keyword, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var q = _db.Permissions.AsNoTracking()
                     .Where(p => !p.PermKey.Contains(".")); // 排除 Accounts.View 這類功能權限

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(p => p.PermKey.Contains(k) || (p.PermName != null && p.PermName.Contains(k)));
            }

            var total = await q.CountAsync();
            var items = await q
                .OrderBy(p => p.PermissionID) // 讓畫面 ID 依建立順序遞增
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new RowVM
                {
                    PermissionID = p.PermissionID,
                    Key = p.PermKey,
                    GrantedUserCount = _db.UserPermissions.Count(up => up.PermissionID == p.PermissionID),
                    FeatureCount = _db.PermissionFeatures.Count(pf => pf.PermissionID == p.PermissionID)
                }).ToListAsync();

            return View(new IndexVM
            {
                Keyword = keyword,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            });
        }

        // 建立新權限（含初始化勾選的 featureIds）
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string key, int[]? featureIds)
        {
            key = (key ?? "").Trim();
            if (string.IsNullOrEmpty(key)) return BadRequest("Key 必填");
            if (key.Contains(".")) return BadRequest("Key 不能包含 '.'");

            if (await _db.Permissions.AnyAsync(p => p.PermKey == key))
                return BadRequest("名稱已存在");

            var p = new Permission { PermKey = key, PermName = key };
            _db.Permissions.Add(p);
            await _db.SaveChangesAsync();

            if (featureIds != null && featureIds.Length > 0)
            {
                foreach (var fid in featureIds.Distinct())
                    _db.PermissionFeatures.Add(new PermissionFeature { PermissionID = p.PermissionID, FeatureID = fid });
                await _db.SaveChangesAsync();
            }

            return Ok(new { ok = true });
        }

        // 重新命名（直接改 PermKey）
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Rename(int id, string newKey)
        {
            newKey = (newKey ?? "").Trim();
            if (string.IsNullOrEmpty(newKey)) return BadRequest("Key 必填");
            if (newKey.Contains(".")) return BadRequest("Key 不能包含 '.'");

            var entity = await _db.Permissions.FirstOrDefaultAsync(p => p.PermissionID == id);
            if (entity == null) return NotFound();

            if (!string.Equals(entity.PermKey, newKey) &&
                await _db.Permissions.AnyAsync(p => p.PermKey == newKey))
                return BadRequest("名稱已存在");

            entity.PermKey = newKey;
            entity.PermName = newKey;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // 刪除（連同關聯）
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.Permissions.FirstOrDefaultAsync(p => p.PermissionID == id);
            if (entity == null) return NotFound();

            var relUsers = _db.UserPermissions.Where(up => up.PermissionID == id);
            var relFeats = _db.PermissionFeatures.Where(pf => pf.PermissionID == id);
            _db.UserPermissions.RemoveRange(relUsers);
            _db.PermissionFeatures.RemoveRange(relFeats);
            _db.Permissions.Remove(entity);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // === 適用帳號 ===
        [HttpGet]
        public async Task<IActionResult> GetAssignedUsers(int id, string? q, int take = 20)
        {
            var assigned = await _db.UserPermissions
                .Where(up => up.PermissionID == id)
                .Join(_db.Users, up => up.UserID, u => u.UserID, (up, u) => new { u.UserID, u.Email, u.Phone })
                .OrderBy(x => x.UserID).ToListAsync();

            var candidates = new object[0];
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                int.TryParse(k, out var uid);
				candidates = await _db.Users.AsNoTracking()
	                .Where(u => u.Email.Contains(k) || (u.Phone != null && u.Phone.Contains(k)) || u.UserID == uid)
	                .OrderBy(u => u.UserID).Take(take)
	                .Select(u => new { u.UserID, u.Email, u.Phone })
	                .Cast<object>()
	                .ToArrayAsync();
			}

			return Json(new { assigned, candidates });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(int id, int userId)
        {
            var exists = await _db.UserPermissions.FirstOrDefaultAsync(x => x.PermissionID == id && x.UserID == userId);
            if (exists == null)
            {
                _db.UserPermissions.Add(new UserPermission { PermissionID = id, UserID = userId });
                await _db.SaveChangesAsync();
            }
            return Ok(new { ok = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(int id, int userId)
        {
            var exists = await _db.UserPermissions.FirstOrDefaultAsync(x => x.PermissionID == id && x.UserID == userId);
            if (exists != null)
            {
                _db.UserPermissions.Remove(exists);
                await _db.SaveChangesAsync();
            }
            return Ok(new { ok = true });
        }

        // === 功能勾選 ===
        [HttpGet]
        public async Task<IActionResult> GetFeatures(int id)
        {
            var all = await _db.Features.AsNoTracking().OrderBy(f => f.Group).ThenBy(f => f.SortOrder).ToListAsync();
            var checkedIds = await _db.PermissionFeatures.AsNoTracking().Where(pf => pf.PermissionID == id).Select(pf => pf.FeatureID).ToListAsync();
            return Json(new
            {
                features = all.Select(f => new { f.FeatureID, f.Code, f.Name, f.Group, f.IsPageLevel }).ToList(),
                checkedIds
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeature(int id, int featureId, bool check)
        {
            var row = await _db.PermissionFeatures.FirstOrDefaultAsync(x => x.PermissionID == id && x.FeatureID == featureId);
            if (check)
            {
                if (row == null)
                {
                    _db.PermissionFeatures.Add(new PermissionFeature { PermissionID = id, FeatureID = featureId });
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                if (row != null)
                {
                    _db.PermissionFeatures.Remove(row);
                    await _db.SaveChangesAsync();
                }
            }
            return Ok(new { ok = true });
        }

        // === VM ===
        public class IndexVM
        {
            public string? Keyword { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int Total { get; set; }
            public System.Collections.Generic.List<RowVM> Items { get; set; } = new();
        }
        public class RowVM
        {
            public int PermissionID { get; set; }
            public string Key { get; set; } = "";
            public int GrantedUserCount { get; set; }
            public int FeatureCount { get; set; }
        }
    }
}
