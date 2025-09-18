using System;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Account.Controllers;

[Authorize(Policy = "Members.View")]
[Area("Account")]
public class MembersController : Controller
{
	private readonly AppDbContext _db;

	public MembersController(AppDbContext db) => _db = db;

	// GET: /Members
	// 支援：keyword、status、page、pageSize、sort、dir（asc/desc）
	public async Task<IActionResult> Index(
		string? keyword,
		int? status,
		int page = 1,
		int pageSize = 20,
		string sort = "id",
		string dir = "desc")
	{
		if (page < 1) page = 1;
		if (pageSize < 1 || pageSize > 100) pageSize = 20;

		var now = DateTime.UtcNow;

		// 基礎查詢 + 條件
		var baseQuery = _db.Members.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(keyword))
		{
			var k = keyword.Trim();
			baseQuery = baseQuery.Where(m =>
				m.Username.Contains(k) ||
				(m.Email != null && m.Email.Contains(k)) ||
				(m.Phone != null && m.Phone.Contains(k)));
		}

		if (status.HasValue)
		{
			baseQuery = baseQuery.Where(m => m.Status == status.Value);
		}

		// 投影：把「是否黑名單中」以子查詢算出（不依賴 View/Keyless）
		var query =
			from m in baseQuery
			select new MemberListVM
			{
				MemberID = m.MemberID,
				Username = m.Username,
				Email = m.Email,
				Phone = m.Phone,
				Status = m.Status,
				TotalBooks = m.TotalBooks,
				TotalBorrows = m.TotalBorrows,
				IsBlacklistedNow = _db.Blacklists.Any(b =>
					b.MemberID == m.MemberID &&
					b.StartAt <= now &&
					(b.EndAt == null || b.EndAt > now) &&
					b.LiftedAt == null)
			};

		// 排序（含黑名單欄位）
		bool asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
		query = sort switch
		{
			"name" => asc ? query.OrderBy(x => x.Username) : query.OrderByDescending(x => x.Username),
			"email" => asc ? query.OrderBy(x => x.Email) : query.OrderByDescending(x => x.Email),
			"phone" => asc ? query.OrderBy(x => x.Phone) : query.OrderByDescending(x => x.Phone),
			"status" => asc ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
			"books" => asc ? query.OrderBy(x => x.TotalBooks) : query.OrderByDescending(x => x.TotalBooks),
			"borrows" => asc ? query.OrderBy(x => x.TotalBorrows) : query.OrderByDescending(x => x.TotalBorrows),
			"black" => asc ? query.OrderBy(x => x.IsBlacklistedNow) : query.OrderByDescending(x => x.IsBlacklistedNow),
			"id" or _ => asc ? query.OrderBy(x => x.MemberID) : query.OrderByDescending(x => x.MemberID),
		};

		// 總數 + 分頁
		var total = await query.CountAsync();
		var items = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		var vm = new MemberIndexVM
		{
			Keyword = keyword,
			Status = status,
			Page = page,
			PageSize = pageSize,
			Total = total,
			Sort = sort,
			Dir = asc ? "asc" : "desc",
			Items = items
		};

		return View(vm);
	}

	// 其餘 CRUD（若你已實作可保留原版）
	[Authorize(Policy = "Members.Edit")]
	public IActionResult Create()
	{
		FillStatusRoleSelect();
		return View(new Member { Status = 1, Role = 0 });
	}

	[Authorize(Policy = "Members.Edit")]
	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(Member m)
	{
		if (string.IsNullOrWhiteSpace(m.Username))
			ModelState.AddModelError(nameof(m.Username), "請輸入暱稱/姓名");

		if (!ModelState.IsValid)
		{
			FillStatusRoleSelect();
			return View(m);
		}

		m.CreatedAt = DateTime.UtcNow;
		m.UpdatedAt = DateTime.UtcNow;

		_db.Members.Add(m);
		try
		{
			await _db.SaveChangesAsync();
			TempData["ok"] = "已建立會員";
			return RedirectToAction(nameof(Index));
		}
		catch
		{
			ModelState.AddModelError(string.Empty, "新增失敗，請檢查 Email / Phone 是否已存在或資料格式。");
			FillStatusRoleSelect();
			return View(m);
		}
	}

	[Authorize(Policy = "Members.Edit")]
	public async Task<IActionResult> Edit(int id)
	{
		var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberID == id);
		if (m == null) return NotFound();
		FillStatusRoleSelect();
		return View(m);
	}

	[Authorize(Policy = "Members.Edit")]
	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int id, Member input)
	{
		if (id != input.MemberID) return BadRequest();

		var entity = await _db.Members.FirstOrDefaultAsync(x => x.MemberID == id);
		if (entity == null) return NotFound();

		if (!ModelState.IsValid)
		{
			FillStatusRoleSelect();
			return View(entity);
		}

		entity.Username = input.Username?.Trim() ?? entity.Username;
		entity.Email = input.Email?.Trim();
		entity.Phone = input.Phone?.Trim();
		entity.Role = input.Role;
		entity.Status = input.Status;
		entity.TotalBooks = input.TotalBooks;
		entity.TotalBorrows = input.TotalBorrows;
		entity.UpdatedAt = DateTime.UtcNow;

		// 併發（若你有 RowVersion）
		try
		{
			await _db.SaveChangesAsync();
			TempData["ok"] = "已儲存變更";
			return RedirectToAction(nameof(Index));
		}
		catch (DbUpdateConcurrencyException)
		{
			ModelState.AddModelError(string.Empty, "此筆資料已被其他人修改，請重新載入後再試。");
			FillStatusRoleSelect();
			return View(entity);
		}
		catch
		{
			ModelState.AddModelError(string.Empty, "儲存失敗，請檢查 Email / Phone 是否重複或格式不正確。");
			FillStatusRoleSelect();
			return View(entity);
		}
	}

	public async Task<IActionResult> Details(int id)
	{
		var m = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == id);
		if (m == null) return NotFound();

		var now = DateTime.UtcNow;
		var isBlack = await _db.Blacklists.AnyAsync(b =>
			b.MemberID == id &&
			b.StartAt <= now &&
			(b.EndAt == null || b.EndAt > now) &&
			b.LiftedAt == null);

		ViewBag.IsBlacklisted = isBlack;
		return View(m);
	}

	[Authorize(Policy = "Members.Edit")]
	public async Task<IActionResult> Delete(int id)
	{
		var m = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == id);
		if (m == null) return NotFound();
		return View(m);
	}

	[Authorize(Policy = "Members.Edit")]
	[HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int id)
	{
		var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberID == id);
		if (m == null) return NotFound();

		_db.Members.Remove(m);
		await _db.SaveChangesAsync();
		TempData["ok"] = "已刪除會員";
		return RedirectToAction(nameof(Index));
	}

	private void FillStatusRoleSelect()
	{
		ViewBag.StatusList = new[]
		{
			new SelectItemVM(0,"未啟用"),
			new SelectItemVM(1,"啟用"),
			new SelectItemVM(2,"停權"),
			new SelectItemVM(3,"關閉")
		};
		ViewBag.RoleList = new[]
		{
			new SelectItemVM(0,"一般"),
			new SelectItemVM(1,"管理會員")
		};
	}

	// === VM ===
	public record SelectItemVM(int Value, string Text);

	public class MemberIndexVM
	{
		public string? Keyword { get; set; }
		public int? Status { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int Total { get; set; }
		public string Sort { get; set; } = "id";
		public string Dir { get; set; } = "desc";
		public System.Collections.Generic.List<MemberListVM> Items { get; set; } = new();
	}

	public class MemberListVM
	{
		public int MemberID { get; set; }
		public string Username { get; set; } = null!;
		public string? Email { get; set; }
		public string? Phone { get; set; }
		public byte Status { get; set; }
		public int TotalBooks { get; set; }
		public int TotalBorrows { get; set; }
		public bool IsBlacklistedNow { get; set; }
	}
}
