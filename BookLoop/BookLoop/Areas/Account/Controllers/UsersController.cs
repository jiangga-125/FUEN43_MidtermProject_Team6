using System;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop; // User
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Services;

namespace Account.Controllers
{
	// 基底含 [Area("Account")] + [Authorize(Policy="Account.Access")]
	public class UsersController : AccountAreaController
	{
		private readonly AppDbContext _db;
		public UsersController(AppDbContext db) => _db = db;

		private async Task<UserIndexVM> BuildIndexVm(string? keyword, int? status, string? sort, string? dir, int page, int pageSize)
		{
			if (page < 1) page = 1;
			if (pageSize < 1 || pageSize > 100) pageSize = 20;

			ViewBag.StatusList = new[]
			{
				new SelectListItem("啟用","1"),
				new SelectListItem("停用","2")
			};

			var q = _db.Users.AsNoTracking();

			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var k = keyword.Trim();
				q = q.Where(x =>
					x.Email.Contains(k) ||
					(x.Phone != null && x.Phone.Contains(k)) ||
					(x.Name != null && x.Name.Contains(k)));
			}

			if (status.HasValue) q = q.Where(x => x.Status == status.Value);

			var isDesc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
			q = (sort?.ToLowerInvariant()) switch
			{
				"name" => (isDesc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name)),
				"email" => (isDesc ? q.OrderByDescending(x => x.Email) : q.OrderBy(x => x.Email)),
				"phone" => (isDesc ? q.OrderByDescending(x => x.Phone) : q.OrderBy(x => x.Phone)),
				"status" => (isDesc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status)),
				"usertype" => (isDesc ? q.OrderByDescending(x => x.UserType) : q.OrderBy(x => x.UserType)),
				_ => (isDesc ? q.OrderByDescending(x => x.UserID) : q.OrderBy(x => x.UserID)),
			};

			var total = await q.CountAsync();
			var items = await q
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(x => new UserListVM
				{
					UserID = x.UserID,
					Name = x.Name,
					Email = x.Email,
					Phone = x.Phone,
					Status = x.Status,
					UserType = x.UserType,
					LastLoginAt = x.LastLoginAt
				})
				.ToListAsync();

			ViewBag.Sort = (sort?.ToLowerInvariant() ?? "id");
			ViewBag.Dir = isDesc ? "desc" : "asc";

			return new UserIndexVM
			{
				Keyword = keyword,
				Status = status,
				Page = page,
				PageSize = pageSize,
				Total = total,
				Items = items
			};
		}

		// ===== 列表 / 局部列表 / 詳細：Users.View =====
		[Authorize(Policy = "Users.View")]
		public async Task<IActionResult> Index(string? keyword, int? status, string? sort = "id", string? dir = "asc",
											   int page = 1, int pageSize = 20)
			=> View(await BuildIndexVm(keyword, status, sort, dir, page, pageSize));

		[HttpGet, Authorize(Policy = "Users.View")]
		public async Task<IActionResult> List(string? keyword, int? status, string? sort = "id", string? dir = "asc",
											  int page = 1, int pageSize = 20)
			=> PartialView("_UsersList", await BuildIndexVm(keyword, status, sort, dir, page, pageSize));

		[Authorize(Policy = "Users.View")]
		public async Task<IActionResult> Details(int id)
		{
			var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
			if (u == null) return NotFound();
			return View(u);
		}

		// ===== 建立：Users.Create =====
		[Authorize(Policy = "Users.Create")]
		public IActionResult Create()
		{
			FillUserTypeStatus();
			return View(new UserFormVm { Status = 1, UserType = 2 });
		}

		[Authorize(Policy = "Users.Create")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(UserFormVm vm)
		{
			if (!ModelState.IsValid)
			{
				FillUserTypeStatus();
				return View(vm);
			}

			var entity = new User
			{
				Email = vm.Email.Trim(),
				Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
				Name = string.IsNullOrWhiteSpace(vm.Name) ? null : vm.Name.Trim(),
				Status = vm.Status,
				UserType = vm.UserType,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow,
				PasswordHash = PasswordHasher.Hash("Init@12345!"),
				MustChangePassword = true
			};

			_db.Users.Add(entity);
			try
			{
				await _db.SaveChangesAsync();
				TempData["ok"] = "已新增後台帳號";
				return RedirectToAction(nameof(Index));
			}
			catch (DbUpdateException)
			{
				ModelState.AddModelError(nameof(vm.Email), "新增失敗，請確認 Email 是否重複。");
				FillUserTypeStatus();
				return View(vm);
			}
		}

		// ===== 編輯：Users.Edit =====
		[Authorize(Policy = "Users.Edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
			if (u == null) return NotFound();
			FillUserTypeStatus();

			var vm = new UserFormVm
			{
				UserID = u.UserID,
				Email = u.Email,
				Phone = u.Phone,
				Name = u.Name,
				Status = u.Status,
				UserType = u.UserType
			};
			return View(vm);
		}

		[Authorize(Policy = "Users.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, UserFormVm vm)
		{
			if (id != vm.UserID) return BadRequest();
			var entity = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
			if (entity == null) return NotFound();

			if (!ModelState.IsValid)
			{
				FillUserTypeStatus();
				return View(vm);
			}

			entity.Email = vm.Email?.Trim() ?? entity.Email;
			entity.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
			entity.Name = string.IsNullOrWhiteSpace(vm.Name) ? null : vm.Name.Trim();
			entity.Status = vm.Status;
			entity.UserType = vm.UserType;
			entity.UpdatedAt = DateTime.UtcNow;

			try
			{
				await _db.SaveChangesAsync();
				TempData["ok"] = "已儲存變更";
				return RedirectToAction(nameof(Index));
			}
			catch (DbUpdateException)
			{
				ModelState.AddModelError(nameof(vm.Email), "儲存失敗，請確認 Email 是否重複。");
				FillUserTypeStatus();
				return View(vm);
			}
		}

		// ===== 刪除：Users.Delete =====
		[Authorize(Policy = "Users.Delete")]
		public async Task<IActionResult> Delete(int id)
		{
			var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
			if (u == null) return NotFound();
			return View(u);
		}

		[Authorize(Policy = "Users.Delete")]
		[HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
			if (u == null)
			{
				if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
					return Json(new { ok = false, message = "找不到此帳號。" });
				return NotFound();
			}
			_db.Users.Remove(u);
			await _db.SaveChangesAsync();

			if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
				return Json(new { ok = true, message = "已刪除帳號。" });

			TempData["ok"] = "已刪除帳號";
			return RedirectToAction(nameof(Index));
		}

		private void FillUserTypeStatus()
		{
			ViewBag.UserTypeList = new[]
			{
				new SelectListItem("顧客(前台不使用)","1"),
				new SelectListItem("員工","2"),
				new SelectListItem("書商","3")
			};
			ViewBag.StatusList = new[]
			{
				new SelectListItem("啟用","1"),
				new SelectListItem("停用","2"),
			};
		}

		// ===== VM =====
		public class UserIndexVM
		{
			public string? Keyword { get; set; }
			public int? Status { get; set; }
			public int Page { get; set; }
			public int PageSize { get; set; }
			public int Total { get; set; }
			public System.Collections.Generic.List<UserListVM> Items { get; set; } = new();
		}
		public class UserListVM
		{
			public int UserID { get; set; }
			public string? Name { get; set; }
			public string Email { get; set; } = "";
			public string? Phone { get; set; }
			public byte Status { get; set; }
			public byte UserType { get; set; }
			public DateTime? LastLoginAt { get; set; }
		}
	}
}
