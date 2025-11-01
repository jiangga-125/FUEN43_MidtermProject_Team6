using BookLoop;
using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Account.Controllers
{
	// 假設你的 AccountAreaController 已含 [Area("Account")] 與門票
	public class BlacklistsController : AccountAreaController
	{
		private readonly AppDbContext _db;
		public BlacklistsController(AppDbContext db) => _db = db;

		private async Task<BlacklistIndexVM> BuildIndexVm(string? keyword, int? status, string? sort, string? dir, int page, int pageSize)
		{
			if (page < 1) page = 1;
			if (pageSize < 1 || pageSize > 100) pageSize = 20;

			ViewBag.StatusList = new[]
			{
				new SelectListItem("全部狀態",""),
				new SelectListItem("生效中","1"),
				new SelectListItem("非生效","0"),
			};

			var nowUtc = DateTime.UtcNow;

			// LEFT JOIN：即使會員被刪除也能看到黑名單
			var q = from b in _db.Blacklists.AsNoTracking()
					join mm in _db.Members.AsNoTracking() on b.MemberID equals mm.MemberID into gj
					from m in gj.DefaultIfEmpty()
					select new Row
					{
						BlacklistID = b.BlacklistID,
						MemberID = b.MemberID,
						MemberName = m != null ? m.Username : "(會員已刪除)",
						Email = m != null ? m.Email : null,
						Phone = m != null ? m.Phone : null,
						Reason = b.Reason,
						StartAt = b.StartAt,
						EndAt = b.EndAt,
						LiftedAt = b.LiftedAt,
						IsActive = b.StartAt <= nowUtc && (b.EndAt == null || b.EndAt > nowUtc) && b.LiftedAt == null
					};

			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var k = keyword.Trim();
				q = q.Where(x =>
					(x.MemberName != null && x.MemberName.Contains(k)) ||
					(x.Email != null && x.Email.Contains(k)) ||
					(x.Phone != null && x.Phone.Contains(k)) ||
					(x.Reason != null && x.Reason.Contains(k)));
			}

			if (status.HasValue)
			{
				bool active = status.Value == 1;
				q = q.Where(x => x.IsActive == active);
			}

			var isDesc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
			q = (sort?.ToLowerInvariant()) switch
			{
				"member" => isDesc ? q.OrderByDescending(x => x.MemberName) : q.OrderBy(x => x.MemberName),
				"email" => isDesc ? q.OrderByDescending(x => x.Email) : q.OrderBy(x => x.Email),
				"start" => isDesc ? q.OrderByDescending(x => x.StartAt) : q.OrderBy(x => x.StartAt),
				"end" => isDesc ? q.OrderByDescending(x => x.EndAt) : q.OrderBy(x => x.EndAt),
				"active" => isDesc ? q.OrderByDescending(x => x.IsActive) : q.OrderBy(x => x.IsActive),
				_ => isDesc ? q.OrderByDescending(x => x.BlacklistID) : q.OrderBy(x => x.BlacklistID),
			};

			var total = await q.CountAsync();
			var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

			ViewBag.Sort = sort?.ToLowerInvariant() ?? "id";
			ViewBag.Dir = isDesc ? "desc" : "asc";

			return new BlacklistIndexVM
			{
				Keyword = keyword,
				Status = status,
				Page = page,
				PageSize = pageSize,
				Total = total,
				Items = items
			};
		}

		// ===== List / Index（需要 View）=====
		[Authorize(Policy = "Blacklists.View")]
		public async Task<IActionResult> Index(string? keyword, int? status, string? sort = "id", string? dir = "desc",
											   int page = 1, int pageSize = 20)
		{
			var vm = await BuildIndexVm(keyword, status, sort, dir, page, pageSize);
			return View(vm);
		}

		[HttpGet]
		[Authorize(Policy = "Blacklists.View")]
		public async Task<IActionResult> List(string? keyword, int? status, string? sort = "id", string? dir = "desc",
											  int page = 1, int pageSize = 20)
		{
			var vm = await BuildIndexVm(keyword, status, sort, dir, page, pageSize);
			return PartialView("_BlacklistsList", vm);
		}

		// ===== Details（與 View 同權）=====
		[Authorize(Policy = "Blacklists.View")]
		public async Task<IActionResult> Details(int id)
		{
			var b = await _db.Blacklists.AsNoTracking().FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();
			ViewBag.Member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == b.MemberID);
			return View(b);
		}

		// ===== 建立 =====
		[Authorize(Policy = "Blacklists.Manage")]
		public async Task<IActionResult> Create(int memberId)
		{
            // 1) 基本參數檢查
            if (memberId <= 0) return BadRequest("缺少或錯誤的 memberId。");

            // 2) 驗證會員是否存在
            var memberExists = await _db.Members.AnyAsync(m => m.MemberID == memberId);
            if (!memberExists) return NotFound("找不到此會員。");
            
			return View(new Blacklist
			{
				MemberID = memberId,
				StartAt = DateTime.UtcNow,
				EndAt = null
			});
		}
       

		[Authorize(Policy = "Blacklists.Create")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Blacklist input)
		{
			if (input.MemberID <= 0)
				ModelState.AddModelError(nameof(input.MemberID), "請選擇會員");
			if (input.EndAt.HasValue && input.EndAt <= input.StartAt)
				ModelState.AddModelError(nameof(input.EndAt), "結束時間需大於開始時間");

			if (!ModelState.IsValid)
			{
				await FillMembersSelect(input.MemberID);
				return View(input);
			}

			input.CreatedAt = DateTime.UtcNow;
			_db.Blacklists.Add(input);
			await _db.SaveChangesAsync();
			TempData["ok"] = "已加入黑名單";
			return RedirectToAction(nameof(Index));
		}

		// ===== Edit =====
		[Authorize(Policy = "Blacklists.Edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var b = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();
			await FillMembersSelect(b.MemberID);
			return View(b);
		}

		[Authorize(Policy = "Blacklists.Edit")]
		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Blacklist input)
		{
			if (id != input.BlacklistID) return BadRequest();
			var entity = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (entity == null) return NotFound();

			if (input.EndAt.HasValue && input.EndAt <= input.StartAt)
				ModelState.AddModelError(nameof(input.EndAt), "結束時間需大於開始時間");

			if (!ModelState.IsValid)
			{
				await FillMembersSelect(input.MemberID);
				return View(entity);
			}

			entity.MemberID = input.MemberID;
			entity.Reason = input.Reason;
			entity.SourceType = input.SourceType;
			entity.StartAt = input.StartAt;
			entity.EndAt = input.EndAt;
			entity.LiftedAt = input.LiftedAt;

			await _db.SaveChangesAsync();
			TempData["ok"] = "已更新黑名單";
			return RedirectToAction(nameof(Index));
		}

		// ===== Delete =====
		[Authorize(Policy = "Blacklists.Delete")]
		public async Task<IActionResult> Delete(int id)
		{
			var b = await _db.Blacklists.AsNoTracking().FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();
			ViewBag.Member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == b.MemberID);
			return View(b);
		}

		[Authorize(Policy = "Blacklists.Delete")]
		[HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var b = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();

			_db.Blacklists.Remove(b);
			await _db.SaveChangesAsync();
			TempData["ok"] = "已刪除黑名單記錄";
			return RedirectToAction(nameof(Index));
		}

		private async Task FillMembersSelect(int? selectedId = null)
		{
			var members = await _db.Members
				.AsNoTracking()
				.OrderBy(x => x.Username)
				.Select(x => new { x.MemberID, Name = x.Username + (x.Email != null ? $" ({x.Email})" : "") })
				.ToListAsync();

			ViewBag.MemberSelect = members.Select(x =>
				new SelectListItem(x.Name, x.MemberID.ToString(), selectedId.HasValue && selectedId.Value == x.MemberID)
			).ToList();
		}

		// ===== VM =====
		public class BlacklistIndexVM
		{
			public string? Keyword { get; set; }
			public int? Status { get; set; } // 1=生效中, 0=非生效
			public int Page { get; set; }
			public int PageSize { get; set; }
			public int Total { get; set; }
			public List<Row> Items { get; set; } = new();
		}

		public class Row
		{
			public int BlacklistID { get; set; }
			public int MemberID { get; set; }
			public string? MemberName { get; set; }
			public string? Email { get; set; }
			public string? Phone { get; set; }
			public string? Reason { get; set; }
			public DateTime StartAt { get; set; }
			public DateTime? EndAt { get; set; }
			public DateTime? LiftedAt { get; set; }
			public bool IsActive { get; set; }
		}
	}
}
