using System;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Services;

namespace Account.Controllers;

[Authorize(Policy = "Accounts.View")]
[Area("Account")]
public class UsersController : Controller
{
	private readonly AppDbContext _db;
	public UsersController(AppDbContext db) => _db = db;

	private async Task<UserIndexVM> BuildIndexVm(string? keyword, int? status, string? sort, string? dir, int page, int pageSize)
	{
		if (page < 1) page = 1;
		if (pageSize < 1 || pageSize > 100) pageSize = 20;

		ViewBag.StatusList = new[]
		{
			new SelectListItem("全部狀態",""),
			new SelectListItem("未啟用","0"),
			new SelectListItem("啟用","1"),
			new SelectListItem("停用","2")
		};

		var q = _db.Users.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(keyword))
		{
			var k = keyword.Trim();
			q = q.Where(x =>
				x.Email.Contains(k) ||
				(x.Phone != null && x.Phone.Contains(k)));
		}
		if (status.HasValue) q = q.Where(x => x.Status == status.Value);

		var isDesc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
		q = (sort?.ToLowerInvariant()) switch
		{
			"email" => (isDesc ? q.OrderByDescending(x => x.Email) : q.OrderBy(x => x.Email)),
			"phone" => (isDesc ? q.OrderByDescending(x => x.Phone) : q.OrderBy(x => x.Phone)),
			"status" => (isDesc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status)),
			_ => (isDesc ? q.OrderByDescending(x => x.UserID) : q.OrderBy(x => x.UserID)),
		};

		var total = await q.CountAsync();
		var items = await q
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new UserListVM
			{
				UserID = x.UserID,
				Email = x.Email,
				Phone = x.Phone,
				Status = x.Status,
				UserType = x.UserType,
				LastLoginAt = x.LastLoginAt
			})
			.ToListAsync();

		ViewBag.Sort = sort?.ToLowerInvariant() ?? "id";
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

	public async Task<IActionResult> Index(string? keyword, int? status, string? sort = "id", string? dir = "desc",
										   int page = 1, int pageSize = 20)
	{
		var vm = await BuildIndexVm(keyword, status, sort, dir, page, pageSize);
		return View(vm);
	}

	[HttpGet]
	public async Task<IActionResult> List(string? keyword, int? status, string? sort = "id", string? dir = "desc",
										  int page = 1, int pageSize = 20)
	{
		var vm = await BuildIndexVm(keyword, status, sort, dir, page, pageSize);
		return PartialView("_UsersList", vm);
	}

	// ===== 詳細 =====
	public async Task<IActionResult> Details(int id)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
		if (u == null) return NotFound();
		return View(u);
	}

	// ===== 建立 =====
	[Authorize(Policy = "Accounts.Edit")]
	public IActionResult Create()
	{
		FillUserTypeStatus();
		return View(new User { Status = 1, UserType = 2 });
	}

	[Authorize(Policy = "Accounts.Edit")]
	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(User input)
	{
		if (string.IsNullOrWhiteSpace(input.Email))
			ModelState.AddModelError(nameof(input.Email), "請輸入 Email");

		if (!ModelState.IsValid)
		{
			FillUserTypeStatus();
			return View(input);
		}

		input.Email = input.Email.Trim();
		input.Phone = input.Phone?.Trim();
		input.CreatedAt = DateTime.UtcNow;
		input.UpdatedAt = DateTime.UtcNow;

		// 密碼：若未提供，給預設並強迫改密碼（可依需求調整）
		if (string.IsNullOrWhiteSpace(input.PasswordHash))
		{
			input.PasswordHash = PasswordHasher.Hash("Init@12345!");
			input.MustChangePassword = true;
		}

		_db.Users.Add(input);
		try
		{
			await _db.SaveChangesAsync();
			TempData["ok"] = "已新增後台帳號";
			return RedirectToAction(nameof(Index));
		}
		catch (DbUpdateException)
		{
			ModelState.AddModelError(string.Empty, "新增失敗，請確認 Email 是否重複。");
			FillUserTypeStatus();
			return View(input);
		}
	}

	// ===== 編輯 =====
	[Authorize(Policy = "Accounts.Edit")]
	public async Task<IActionResult> Edit(int id)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
		if (u == null) return NotFound();
		FillUserTypeStatus();
		return View(u);
	}

	[Authorize(Policy = "Accounts.Edit")]
	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int id, User input)
	{
		if (id != input.UserID) return BadRequest();

		var entity = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
		if (entity == null) return NotFound();

		if (!ModelState.IsValid)
		{
			FillUserTypeStatus();
			return View(entity);
		}

		entity.Email = input.Email?.Trim() ?? entity.Email;
		entity.Phone = input.Phone?.Trim();
		entity.Status = input.Status;
		entity.UserType = input.UserType;
		entity.UpdatedAt = DateTime.UtcNow;

		try
		{
			await _db.SaveChangesAsync();
			TempData["ok"] = "已儲存變更";
			return RedirectToAction(nameof(Index));
		}
		catch (DbUpdateException)
		{
			ModelState.AddModelError(string.Empty, "儲存失敗，請確認 Email 是否重複。");
			FillUserTypeStatus();
			return View(entity);
		}
	}

	// ===== 刪除 =====
	[Authorize(Policy = "Accounts.Edit")]
	public async Task<IActionResult> Delete(int id)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
		if (u == null) return NotFound();
		return View(u);
	}

	[Authorize(Policy = "Accounts.Edit")]
	[HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int id)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
		if (u == null) return NotFound();

		_db.Users.Remove(u);
		await _db.SaveChangesAsync();
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
			new SelectListItem("未啟用","0"),
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
		public string Email { get; set; } = "";
		public string? Phone { get; set; }
		public byte Status { get; set; }
		public byte UserType { get; set; }
		public DateTime? LastLoginAt { get; set; }
	}
}
