using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop; // 你原本就有，用來找到 Permission/Feature 等實體
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Account.Controllers
{
	// ✅ 改為繼承 Base：門票與 Area 由 Base 統一處理
	public class PermissionsController : AccountAreaController
	{
		private readonly AppDbContext _db;
		public PermissionsController(AppDbContext db) => _db = db;

		// === 清單頁 ===
		// 看清單：Permissions.Index
		[Authorize(Policy = "Permissions.Index")]
		public async Task<IActionResult> Index(string? keyword, int page = 1, int pageSize = 10)
		{
			if (page < 1) page = 1;
			if (pageSize < 1 || pageSize > 100) pageSize = 10;

			var q = _db.Permissions.AsNoTracking()
					 .Where(p => !p.PermKey.Contains(".")); // 排除 Accounts.* 這類功能權限

			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var k = keyword.Trim();
				q = q.Where(p => p.PermKey.Contains(k) || (p.PermName != null && p.PermName.Contains(k)));
			}

			var total = await q.CountAsync();
			var items = await q
				.OrderBy(p => p.PermissionID)
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

		// === 建立/重新命名/刪除（管理集合本身） ===
		// 管理集合：Permissions.Assign
		[Authorize(Policy = "Permissions.Assign")]
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

		[Authorize(Policy = "Permissions.Assign")]
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

		[Authorize(Policy = "Permissions.Assign")]
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

		// === 適用帳號（把集合指派給使用者） ===
		// 指派帳號：Permissions.Assign
		[Authorize(Policy = "Permissions.Assign")]
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

		[Authorize(Policy = "Permissions.Assign")]
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

		[Authorize(Policy = "Permissions.Assign")]
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

		// === 綁定功能（把 feature 勾給集合） ===
		// 讀取/勾選功能：Permissions.Features
		[Authorize(Policy = "Permissions.Features")]
		[HttpGet]
		public async Task<IActionResult> GetFeatures(int id)
		{
			try
			{
				var features = await _db.Set<Feature>().AsNoTracking()
					.OrderBy(f => f.FeatureGroup ?? "")
					.ThenBy(f => f.SortOrder)
					.ThenBy(f => f.Code ?? "")
					.Select(f => new
					{
						featureID = f.FeatureID,
						code = f.Code ?? "",
						name = f.Name ?? "",
						featureGroup = f.FeatureGroup ?? "",
						isPageLevel = f.IsPageLevel
					})
					.ToListAsync();

				var checkedIds = await _db.Set<PermissionFeature>().AsNoTracking()
					.Where(x => x.PermissionID == id)
					.Select(x => x.FeatureID)
					.ToListAsync();

				return Json(new { features, checkedIds });
			}
			catch (System.Exception ex)
			{
				Response.StatusCode = 500;
				return Content(ex.ToString());
			}
		}

		[Authorize(Policy = "Permissions.Features")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleFeature(int id, int featureId, bool check)
		{
			try
			{
				var set = _db.Set<PermissionFeature>();
				var row = await set.FirstOrDefaultAsync(x => x.PermissionID == id && x.FeatureID == featureId);

				if (check)
				{
					if (row == null)
					{
						await set.AddAsync(new PermissionFeature { PermissionID = id, FeatureID = featureId });
						await _db.SaveChangesAsync();
					}
				}
				else
				{
					if (row != null)
					{
						set.Remove(row);
						await _db.SaveChangesAsync();
					}
				}
				return Ok();
			}
			catch (System.Exception ex)
			{
				Response.StatusCode = 500;
				return Content(ex.ToString());
			}
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
