using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop;
using BookLoop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Account.Controllers
{
	// ✅ 已由 Base 帶入 [Area("Account")] 與門票檢查
	public class BlacklistsController : AccountAreaController
	{
		private readonly AppDbContext _db;
		public BlacklistsController(AppDbContext db) => _db = db;

		// ========================= 共用 VM =========================
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

		// ========================= 下拉資料：原因 =========================
		private async Task LoadReasonsAsync(string? selected = null)
		{
			// 1) 優先用罰則表（PenaltyRule）中的原因
			// 若沒有此表，則 fallback 用 Blacklists 的既有原因 + 一些常用預設
			IEnumerable<string> reasons;

			if (_db.GetType().GetProperty("PenaltyRules") != null)
			{
				// 假設 PenaltyRule 有欄位 Reason
				var src = await _db.Set<PenaltyRule>()
								   .AsNoTracking()
								   .Select(r => r.Reason)
								   .Where(r => r != null && r != "")
								   .Distinct()
								   .OrderBy(r => r)
								   .ToListAsync();
				reasons = src;
			}
			else
			{
				var existing = await _db.Blacklists.AsNoTracking()
								  .Select(b => b.Reason)
								  .Where(r => r != null && r != "")
								  .Distinct().ToListAsync();

				var defaults = new[] { "惡意下單", "多次未取貨", "濫用優惠", "違反平台規範" };
				reasons = existing.Union(defaults, StringComparer.OrdinalIgnoreCase)
								  .OrderBy(r => r);
			}

			ViewBag.Reasons = reasons
				.Select(r => new SelectListItem(r, r, string.Equals(r, selected, StringComparison.OrdinalIgnoreCase)))
				.ToList();
		}

		// ========================= 會員下拉（僅供編輯唯讀顯示用） =========================
		private async Task FillMembersSelect(int? selectedId = null)
		{
			var members = await _db.Members
				.AsNoTracking()
				.OrderBy(x => x.Username)
				.Select(x => new { x.MemberID, Name = x.Username }) // 不帶 Email（依你的需求）
				.ToListAsync();

			ViewBag.MemberSelect = members.Select(x =>
				new SelectListItem(x.Name, x.MemberID.ToString(), selectedId.HasValue && selectedId.Value == x.MemberID)
			).ToList();
		}

		// ========================= Index/List =========================
		private async Task<BlacklistIndexVM> BuildIndexVm(string? keyword, int? status, string? sort, string? dir, int page, int pageSize)
		{
			if (page < 1) page = 1;
			if (pageSize is < 1 or > 100) pageSize = 20;

			ViewBag.StatusList = new[]
			{
				new SelectListItem("全部狀態",""),
				new SelectListItem("生效中","1"),
				new SelectListItem("非生效","0"),
			};

			var nowUtc = DateTime.UtcNow;

			var q =
				from b in _db.Blacklists.AsNoTracking()
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

		[HttpGet]
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

		// ========================= Details =========================
		[HttpGet]
		[Authorize(Policy = "Blacklists.View")]
		public async Task<IActionResult> Details(int id)
		{
			var b = await _db.Blacklists.AsNoTracking().FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();
			ViewBag.Member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == b.MemberID);
			return View(b);
		}

		// ========================= Create =========================
		[HttpGet]
		[Authorize(Policy = "Blacklists.Create")]
		public async Task<IActionResult> Create(int? memberId)
		{
			if (memberId.HasValue && memberId.Value > 0)
			{
				var exists = await _db.Members.AsNoTracking().AnyAsync(m => m.MemberID == memberId.Value);
				if (!exists) memberId = null;
			}

			await LoadReasonsAsync(null); // 👉 原因下拉

			var model = new Blacklist
			{
				MemberID = memberId ?? 0,
				StartAt = DateTime.UtcNow.Date,
				EndAt = null
			};
			return View(model);
		}

		[HttpPost, ValidateAntiForgeryToken]
		[Authorize(Policy = "Blacklists.Create")]
		public async Task<IActionResult> Create(Blacklist input)
		{
			if (input.MemberID <= 0)
				ModelState.AddModelError(nameof(input.MemberID), "請選擇會員");
			if (input.EndAt.HasValue && input.EndAt.Value.Date <= input.StartAt.Date)
				ModelState.AddModelError(nameof(input.EndAt), "結束日期需大於開始日期");

			if (!ModelState.IsValid)
			{
				await LoadReasonsAsync(input.Reason);
				return View(input);
			}

			// 前端用 <input type="date">，視為本地 00:00，統一轉成 UTC 保存
			input.StartAt = DateTime.SpecifyKind(input.StartAt.Date, DateTimeKind.Local).ToUniversalTime();
			if (input.EndAt.HasValue)
				input.EndAt = DateTime.SpecifyKind(input.EndAt.Value.Date, DateTimeKind.Local).ToUniversalTime();

			input.CreatedAt = DateTime.UtcNow;

			_db.Blacklists.Add(input);
			await _db.SaveChangesAsync();

			TempData["ok"] = "已加入黑名單";
			return RedirectToAction(nameof(Index));
		}

		// ========================= Edit =========================
		[HttpGet]
		[Authorize(Policy = "Blacklists.Edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var b = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();

			await FillMembersSelect(b.MemberID);      // 用於唯讀顯示會員名
			await LoadReasonsAsync(b.Reason);         // 👉 原因下拉

			// 將 UTC 轉本地日期，交給 <input type="date">
			b.StartAt = b.StartAt.ToLocalTime().Date;
			if (b.EndAt.HasValue) b.EndAt = b.EndAt.Value.ToLocalTime().Date;
			if (b.LiftedAt.HasValue) b.LiftedAt = b.LiftedAt.Value.ToLocalTime().Date;

			return View(b);
		}

		[HttpPost, ValidateAntiForgeryToken]
		[Authorize(Policy = "Blacklists.Edit")]
		public async Task<IActionResult> Edit(int id, Blacklist input)
		{
			if (id != input.BlacklistID) return BadRequest();
			var entity = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (entity == null) return NotFound();

			if (input.EndAt.HasValue && input.EndAt.Value.Date <= input.StartAt.Date)
				ModelState.AddModelError(nameof(input.EndAt), "結束日期需大於開始日期");

			if (!ModelState.IsValid)
			{
				await FillMembersSelect(input.MemberID);
				await LoadReasonsAsync(input.Reason);
				return View(input);
			}

			// 日期→UTC 00:00
			var startUtc = DateTime.SpecifyKind(input.StartAt.Date, DateTimeKind.Local).ToUniversalTime();
			DateTime? endUtc = input.EndAt.HasValue
				? DateTime.SpecifyKind(input.EndAt.Value.Date, DateTimeKind.Local).ToUniversalTime()
				: (DateTime?)null;
			DateTime? liftedUtc = input.LiftedAt.HasValue
				? DateTime.SpecifyKind(input.LiftedAt.Value.Date, DateTimeKind.Local).ToUniversalTime()
				: (DateTime?)null;

			entity.MemberID = input.MemberID;   // 仍保存，不在前端編輯
			entity.Reason = input.Reason;
			entity.SourceType = input.SourceType;
			entity.StartAt = startUtc;
			entity.EndAt = endUtc;
			entity.LiftedAt = liftedUtc;

			await _db.SaveChangesAsync();
			TempData["ok"] = "已更新黑名單";
			return RedirectToAction(nameof(Index));
		}

		// ========================= Delete =========================
		[HttpGet]
		[Authorize(Policy = "Blacklists.Delete")]
		public async Task<IActionResult> Delete(int id)
		{
			var b = await _db.Blacklists.AsNoTracking().FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();
			ViewBag.Member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.MemberID == b.MemberID);
			return View(b);
		}

		[HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
		[Authorize(Policy = "Blacklists.Delete")]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var b = await _db.Blacklists.FirstOrDefaultAsync(x => x.BlacklistID == id);
			if (b == null) return NotFound();

			_db.Blacklists.Remove(b);
			await _db.SaveChangesAsync();

			TempData["ok"] = "已刪除黑名單記錄";
			return RedirectToAction(nameof(Index));
		}
	}

	// 假定存在於同組件中的罰則實體（若你的命名不同，請改成實際型別）
	public class PenaltyRule
	{
		public int PenaltyRuleID { get; set; }
		public string Reason { get; set; } = "";
	}
}
