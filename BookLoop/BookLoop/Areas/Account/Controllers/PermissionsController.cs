using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using BookLoop;                 // Permission / Feature / User / PermissionFeature / UserPermission
using BookLoop.Data;            // AppDbContext
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Account.Controllers
{
	// 基底：含 [Area("Account")] 與基礎門票
	public class PermissionsController : AccountAreaController
	{
		private readonly AppDbContext _db;
		public PermissionsController(AppDbContext db) => _db = db;

		// ============================== View Models ==============================
		public class IndexVM
		{
			public string? Keyword { get; set; }
			public int Page { get; set; }
			public int PageSize { get; set; }
			public int Total { get; set; }
			public List<RowVM> Items { get; set; } = new();
		}
		public class RowVM
		{
			public int PermissionID { get; set; }
			public string Name { get; set; } = "";
			public string Key { get; set; } = "";
			public int GrantedUserCount { get; set; }
			public int FeatureCount { get; set; }
		}
		public class PermissionFormVm
		{
			public int? PermissionID { get; set; }
			public string Key { get; set; } = "";
			public string? Name { get; set; }
		}
		public class UserPickVM
		{
			public int UserID { get; set; }
			public string Email { get; set; } = "";
			public string? Phone { get; set; }
		}
		public class AssignVM
		{
			public Permission Perm { get; set; } = default!;
			public List<UserPickVM> Assigned { get; set; } = new();
			public List<UserPickVM> Candidates { get; set; } = new();
			public string? Keyword { get; set; }
			public int Page { get; set; }
			public int PageSize { get; set; }
			public int Total { get; set; }
		}
		public class FeatureVM
		{
			public int FeatureID { get; set; }
			public string? Code { get; set; }
			public string? Name { get; set; }

			/// <summary>
			/// 依「Code 的第一段」計算出的最終群組鍵（完全忽略資料庫欄位 FeatureGroup）
			/// Account / Books / Borrow / Orders / Reports / Other
			/// </summary>
			public string GroupKey { get; set; } = "Other";
		}

		// ============================== Helpers ==============================
		private static string ToPermKey(string name)
		{
			name ??= "";
			var s = name.Trim();
			if (string.IsNullOrEmpty(s)) return "PERM";
			s = s.Replace('＿', '_').Replace('－', '-');
			s = Regex.Replace(s, @"\s+", "-");
			s = Regex.Replace(s, @"[^A-Za-z0-9\-_]+", "-");
			s = Regex.Replace(s, @"-+", "-").Trim('-');
			if (string.IsNullOrEmpty(s)) s = "PERM";
			return s.ToUpperInvariant();
		}

		private async Task<string> EnsureUniqueKeyAsync(string wanted, int? excludeId = null)
		{
			var key = wanted;
			var i = 2;
			while (await _db.Permissions.AnyAsync(p => p.PermKey == key && (excludeId == null || p.PermissionID != excludeId.Value)))
				key = $"{wanted}_{i++}";
			return key;
		}

		private async Task<IndexVM> BuildIndexVm(string? keyword, string? sort, string? dir, int page, int pageSize)
		{
			if (page < 1) page = 1;
			if (pageSize < 1 || pageSize > 100) pageSize = 20;

			var q = _db.Permissions.AsNoTracking()
				.Where(p => !p.PermKey.Contains(".")); // 排除功能型權限（如 Accounts.*）

			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var k = keyword.Trim();
				q = q.Where(p =>
					(p.PermName != null && p.PermName.Contains(k)) ||
					p.PermKey.Contains(k));
			}

			var isDesc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
			q = (sort?.ToLowerInvariant()) switch
			{
				"name" => isDesc ? q.OrderByDescending(p => p.PermName) : q.OrderBy(p => p.PermName),
				"key" => isDesc ? q.OrderByDescending(p => p.PermKey) : q.OrderBy(p => p.PermKey),
				_ => isDesc ? q.OrderByDescending(p => p.PermissionID) : q.OrderBy(p => p.PermissionID),
			};

			var baseQ = q.Select(p => new RowVM
			{
				PermissionID = p.PermissionID,
				Name = p.PermName ?? p.PermKey,
				Key = p.PermKey,
				GrantedUserCount = _db.UserPermissions.Count(up => up.PermissionID == p.PermissionID),
				FeatureCount = _db.PermissionFeatures.Count(pf => pf.PermissionID == p.PermissionID)
			});

			var sorted = (sort?.ToLowerInvariant()) switch
			{
				"users" => isDesc ? baseQ.OrderByDescending(x => x.GrantedUserCount) : baseQ.OrderBy(x => x.GrantedUserCount),
				"features" => isDesc ? baseQ.OrderByDescending(x => x.FeatureCount) : baseQ.OrderBy(x => x.FeatureCount),
				_ => baseQ
			};

			var total = await sorted.CountAsync();
			var items = await sorted.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

			ViewBag.Sort = sort?.ToLowerInvariant() ?? "id";
			ViewBag.Dir = isDesc ? "desc" : "asc";

			return new IndexVM
			{
				Keyword = keyword,
				Page = page,
				PageSize = pageSize,
				Total = total,
				Items = items
			};
		}

		// ============================== Index / List ==============================
		[Authorize(Policy = "Permissions.View")]
		public async Task<IActionResult> Index(string? keyword, string? sort = "id", string? dir = "asc",
											   int page = 1, int pageSize = 20)
			=> View(await BuildIndexVm(keyword, sort, dir, page, pageSize));

		[HttpGet, Authorize(Policy = "Permissions.View")]
		public async Task<IActionResult> List(string? keyword, string? sort = "id", string? dir = "asc",
											  int page = 1, int pageSize = 20)
			=> PartialView("_PermissionsList", await BuildIndexVm(keyword, sort, dir, page, pageSize));

		// ============================== Create（Name → Key 同步） ==============================
		[Authorize(Policy = "Permissions.Create")]
		[HttpGet]
		public IActionResult Create() => View(new PermissionFormVm());

		[Authorize(Policy = "Permissions.Create")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(PermissionFormVm input)
		{
			input.Name = (input.Name ?? "").Trim();
			if (string.IsNullOrEmpty(input.Name))
				ModelState.AddModelError(nameof(input.Name), "請輸入名稱");
			if (!ModelState.IsValid) return View(input);

			var wanted = ToPermKey(input.Name!);
			var finalKey = await EnsureUniqueKeyAsync(wanted);

			var p = new Permission { PermKey = finalKey, PermName = input.Name };
			_db.Permissions.Add(p);
			await _db.SaveChangesAsync();

			TempData["ok"] = "已建立權限。";
			return RedirectToAction(nameof(Index));
		}

		[Authorize(Policy = "Permissions.Create")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateAjax([FromForm] string name)
		{
			name = (name ?? "").Trim();
			if (string.IsNullOrEmpty(name)) return BadRequest("請輸入名稱");

			var wanted = ToPermKey(name);
			var finalKey = await EnsureUniqueKeyAsync(wanted);

			var p = new Permission { PermKey = finalKey, PermName = name };
			_db.Permissions.Add(p);
			await _db.SaveChangesAsync();

			return Json(new { ok = true, id = p.PermissionID, name = p.PermName });
		}

		// ============================== Rename Name（AJAX） ==============================
		[Authorize(Policy = "Permissions.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> RenameNameAjax(int id, string name)
		{
			name = (name ?? "").Trim();
			if (string.IsNullOrEmpty(name)) return BadRequest("請輸入名稱");

			var entity = await _db.Permissions.FirstOrDefaultAsync(x => x.PermissionID == id);
			if (entity == null) return NotFound();

			entity.PermName = name;
			var wanted = ToPermKey(name);
			entity.PermKey = await EnsureUniqueKeyAsync(wanted, id);

			await _db.SaveChangesAsync();
			return Ok(new { ok = true, name = entity.PermName, key = entity.PermKey });
		}

		// ============================== Assign ==============================
		[Authorize(Policy = "Permissions.Edit")]
		[HttpGet]
		public async Task<IActionResult> Assign(int id, string? q, int page = 1, int pageSize = 20)
		{
			var p = await _db.Permissions.AsNoTracking().FirstOrDefaultAsync(x => x.PermissionID == id);
			if (p == null) return NotFound();

			var assigned = await _db.UserPermissions.AsNoTracking()
				.Where(up => up.PermissionID == id)
				.Join(_db.Users, up => up.UserID, u => u.UserID,
					(up, u) => new UserPickVM { UserID = u.UserID, Email = u.Email, Phone = u.Phone })
				.OrderBy(x => x.UserID)
				.ToListAsync();

			var assignedIds = assigned.Select(a => a.UserID).ToHashSet();
			var candQ = _db.Users.AsNoTracking().Where(u => !assignedIds.Contains(u.UserID));

			if (!string.IsNullOrWhiteSpace(q))
			{
				var k = q.Trim();
				int.TryParse(k, out var uid);
				candQ = candQ.Where(u =>
					u.Email.Contains(k) || (u.Phone != null && u.Phone.Contains(k)) || u.UserID == uid);
			}

			var total = await candQ.CountAsync();
			var cands = await candQ.OrderBy(u => u.UserID)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(u => new UserPickVM { UserID = u.UserID, Email = u.Email, Phone = u.Phone })
				.ToListAsync();

			var vm = new AssignVM
			{
				Perm = p,
				Assigned = assigned,
				Candidates = cands,
				Keyword = q,
				Page = page,
				PageSize = pageSize,
				Total = total
			};
			return View(vm);
		}

		[Authorize(Policy = "Permissions.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> AddUser(int id, int userId)
		{
			var exists = await _db.UserPermissions.FirstOrDefaultAsync(x => x.PermissionID == id && x.UserID == userId);
			if (exists == null)
			{
				_db.UserPermissions.Add(new UserPermission { PermissionID = id, UserID = userId });
				await _db.SaveChangesAsync();
			}
			TempData["ok"] = "已新增帳號";
			return RedirectToAction(nameof(Assign), new { id });
		}

		[Authorize(Policy = "Permissions.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> RemoveUser(int id, int userId)
		{
			var exists = await _db.UserPermissions.FirstOrDefaultAsync(x => x.PermissionID == id && x.UserID == userId);
			if (exists != null)
			{
				_db.UserPermissions.Remove(exists);
				await _db.SaveChangesAsync();
			}
			TempData["ok"] = "已移除帳號";
			return RedirectToAction(nameof(Assign), new { id });
		}

		// ============================== Features（群組 + 類別 + 批次） ==============================
		[Authorize(Policy = "Permissions.Edit")]
		[HttpGet]
		public async Task<IActionResult> Features(int id, string? q = null)
		{
			var p = await _db.Permissions.AsNoTracking().FirstOrDefaultAsync(x => x.PermissionID == id);
			if (p == null) return NotFound();

			// 先把 Feature 全取出，再在記憶體中計算 GroupKey（避免 EF 翻譯問題 & 完全忽略資料庫欄位 FeatureGroup）
			var all = await _db.Set<Feature>().AsNoTracking()
				.OrderBy(f => f.Code ?? "")
				.ThenBy(f => f.SortOrder)
				.ToListAsync();

			static string ModuleOf(string? code)
				=> string.IsNullOrWhiteSpace(code) ? "Other" : code.Split('.', 2)[0];

			static bool IsReportMail(string? code)
				=> code?.StartsWith("ReportMail.", StringComparison.OrdinalIgnoreCase) == true;

			static string GroupOf(string? code)
			{
				var m = ModuleOf(code);
				if (IsReportMail(code)) return "Reports";
				return m switch
				{
					"Users" or "Permissions" or "Members" or "Blacklists" => "Account",
					"Books" => "Books",
					"Borrow" => "Borrow",
					"Orders" => "Orders",
					_ => "Other"
				};
			}

			var features = all.Select(f => new FeatureVM
			{
				FeatureID = f.FeatureID,
				Code = f.Code,
				Name = f.Name,
				GroupKey = GroupOf(f.Code)
			})
			// 穩定排序：群組 → 動詞 → 名稱
			.OrderBy(f => f.GroupKey)
			.ThenBy(f =>
			{
				var verb = (f.Code ?? "").Split('.', 2) is { Length: 2 } sp ? sp[1] : "";
				return verb.ToLower() switch
				{
					"access" => 0,
					"index" => 1,
					"view" => 2,
					"create" => 3,
					"edit" => 4,
					"delete" => 5,
					"assign" => 6,
					"features" => 7,
					_ => 99
				};
			})
			.ThenBy(f => f.Name ?? "")
			.ToList();

			var checkedIds = await _db.Set<PermissionFeature>().AsNoTracking()
				.Where(x => x.PermissionID == id)
				.Select(x => x.FeatureID)
				.ToListAsync();

			ViewBag.Permission = p;
			ViewBag.CheckedIds = checkedIds;
			ViewBag.Keyword = q;
			return View(features);
		}

		[Authorize(Policy = "Permissions.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleFeature(int id, int featureId, bool check)
		{
			var set = _db.Set<PermissionFeature>();
			var row = await set.FirstOrDefaultAsync(x => x.PermissionID == id && x.FeatureID == featureId);

			if (check && row == null)
				await set.AddAsync(new PermissionFeature { PermissionID = id, FeatureID = featureId });
			else if (!check && row != null)
				set.Remove(row);

			await _db.SaveChangesAsync();
			return Ok(new { ok = true });
		}

		// ============================== Delete（AJAX） ==============================
		[Authorize(Policy = "Permissions.Delete")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAjax(int id)
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
	}
}
