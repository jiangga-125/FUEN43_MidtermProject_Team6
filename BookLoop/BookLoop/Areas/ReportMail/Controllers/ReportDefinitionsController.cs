using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using BookLoop.Models;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    //[Authorize(Roles = "Admin,Marketing")]
public class ReportDefinitionsController : Controller
    {
        private readonly ReportMailDbContext _context;

        public ReportDefinitionsController(ReportMailDbContext context)
        {
            _context = context;
        }

        // GET: ReportMail/ReportDefinitions
        public async Task<IActionResult> Index()
        {
            var list = await _context.ReportDefinitions
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();
            return View(list);
        }

        // GET: ReportMail/ReportDefinitions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var reportDefinition = await _context.ReportDefinitions
                .Include(r => r.ReportFilters.OrderBy(f => f.OrderIndex))
                .FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
            if (reportDefinition == null) return NotFound();
            return View(reportDefinition);
        }

		// GET: ReportMail/ReportDefinitions/Create
		public IActionResult Create(string? category)
		{
			var model = new ReportDefinition();
			if (!string.IsNullOrWhiteSpace(category))
			{
				var c = category.Trim().ToLowerInvariant();
				if (c == "line" || c == "bar" || c == "pie")
					model.Category = c; //  Create.cshtml  <select asp-for="Category"> w]
			}
			return View(model);
		}

		// Ψөӱe FiltersJson Z DTO]uOd ValueJson^
		private class ReportFilterDraft
        {
            public string? FieldName { get; set; }
            public string? DisplayName { get; set; }
            public string? DataType { get; set; }
            public string? Operator { get; set; }
            public string? ValueJson { get; set; }    // 唯一來源
			public string? Options { get; set; }
            public int? OrderIndex { get; set; }
            public bool? IsRequired { get; set; }
            public bool? IsActive { get; set; }
        }
		private bool TryParseFilterDrafts(string? filtersJson, out List<ReportFilterDraft> drafts)
		{
			drafts = new List<ReportFilterDraft>();
			if (string.IsNullOrWhiteSpace(filtersJson))
				return true;

			try
			{
				drafts = JsonSerializer.Deserialize<List<ReportFilterDraft>>(filtersJson) ?? new();
				return true;
			}
			catch (JsonException)
			{
				const string message = "自訂篩選條件格式不正確，請確認輸入內容。";
				ModelState.AddModelError("FiltersJson", message);
				ModelState.AddModelError(string.Empty, message);
				return false;
			}
		}

		// POST: ReportMail/ReportDefinitions/Create
		// 把 BaseKind 納入 Bind；時間戳後端自動補；FiltersJson 會展開為多筆 ReportFilter（只寫 ValueJson）
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[Bind("ReportDefinitionID,ReportName,Category,BaseKind,Description,IsActive,CreatedAt,UpdatedAt")]
			ReportDefinition reportDefinition,
			[FromForm] string? FiltersJson)
		{
			if (!ModelState.IsValid) return View(reportDefinition);

			if (!TryParseFilterDrafts(FiltersJson, out var drafts))
				return View(reportDefinition);
			//保證這兩個欄位標準化（去空白 + 小寫），空值給預設
			reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
			reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

			// （建議）新增一律啟用，避免被 Index() 過濾掉
			reportDefinition.IsActive = true;


			_context.Add(reportDefinition);
			await _context.SaveChangesAsync(); // 先產生 ReportDefinitionID


			if (drafts.Count > 0)
			{
				int order = 1;
				foreach (var d in drafts)
				{
					// 友善的 DisplayName 後援（若前端未給）
					var display = (d.DisplayName ?? d.FieldName) ?? "";
					if (string.IsNullOrWhiteSpace(display))
					{
						display = (d.FieldName ?? "").ToLowerInvariant() switch
						{
							"orderdate" => "日期區間",
							"borrowdate" => "日期區間",
							"categoryid" => "書籍種類",
							"saleprice" => "單本價位",
							"metric" => "指標",
							"orderstatus" => "訂單狀態",
							"orderamount" => "單筆訂單金額",
							_ => "(未命名)"
						};
					}

					_context.ReportFilters.Add(new ReportFilter
					{
						ReportDefinitionID = reportDefinition.ReportDefinitionID,
						FieldName = d.FieldName ?? string.Empty,
						DisplayName = display,
						DataType = (d.DataType ?? "text").Trim().ToLowerInvariant(),
						Operator = (d.Operator ?? "eq").Trim().ToLowerInvariant(),
						ValueJson = d.ValueJson ?? "{}",   //  只寫 ValueJson
						Options = d.Options ?? "{}",
						OrderIndex = d.OrderIndex ?? order++,
						IsRequired = d.IsRequired ?? false,
						IsActive = d.IsActive ?? true
					});
				}
				await _context.SaveChangesAsync();
			}

			return RedirectToAction("Index", "Reports", new { area = "ReportMail" });
		}

		// GET: ReportMail/ReportDefinitions/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();
			var reportDefinition = await _context.ReportDefinitions.FindAsync(id);
			if (reportDefinition == null) return NotFound();
			return View(reportDefinition);
		}

		// POST: ReportMail/ReportDefinitions/Edit/5
		// 把 BaseKind 納入 Bind，並在儲存前刷新 UpdatedAt
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id,
			[Bind("ReportDefinitionID,ReportName,Category,BaseKind,Description,IsActive,CreatedAt,UpdatedAt")]
			ReportDefinition reportDefinition,
			[FromForm] string? FiltersJson // 接收前端組好的 Filter 草稿(JSON)
		)
		{
			if (id != reportDefinition.ReportDefinitionID) return NotFound();
			if (!ModelState.IsValid) return View(reportDefinition);
			if (!TryParseFilterDrafts(FiltersJson, out var drafts))
				return View(reportDefinition);
			// 保證這兩個欄位標準化（去空白 + 小寫），空值給預設
			reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
			reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

			// 一律啟用，避免被 Index() 過濾掉
			reportDefinition.IsActive = true;

			using var tx = await _context.Database.BeginTransactionAsync();
			try
			{   // 1) 先更新 Definition 本體
				_context.Update(reportDefinition);
				await _context.SaveChangesAsync();

				// 2)砍掉舊的Filters(最簡潔、避免比對順序異動)
				var olds = _context.ReportFilters.Where(f => f.ReportDefinitionID == reportDefinition.ReportDefinitionID);
				_context.ReportFilters.RemoveRange(olds);
				await _context.SaveChangesAsync();

				// 3)還原新增十的「草稿解析」:逐筆加入新的Filters
				if (drafts.Count > 0)
				{
					int order = 1;
					foreach (var d in drafts)
					{
						if (string.IsNullOrWhiteSpace(d.FieldName)) continue;
						var f = new ReportFilter
						{
							ReportDefinitionID = reportDefinition.ReportDefinitionID,
							FieldName = d.FieldName!.Trim(),
							DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.FieldName!.Trim() : d.DisplayName!.Trim(),
							DataType = (d.DataType ?? "text").Trim().ToLowerInvariant(),
							Operator = (d.Operator ?? "eq").Trim().ToLowerInvariant(),
							ValueJson = d.ValueJson ?? "{}",   // 新版只用 ValueJson
							Options = d.Options ?? "{}",
							OrderIndex = order++,
							IsRequired = d.IsRequired ?? false,   // 或 d.IsRequired.GetValueOrDefault(false)
							IsActive = true
						};
						_context.ReportFilters.Add(f);
					}
					await _context.SaveChangesAsync();
				}
				await tx.CommitAsync();
				return RedirectToAction("Index", "Reports", new { area = "ReportMail" });
			}
			catch (DbUpdateConcurrencyException)
			{
				await tx.RollbackAsync();
				throw;
			}
		}

		// GET: ReportMail/ReportDefinitions/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();
			var reportDefinition = await _context.ReportDefinitions
				.FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
			if (reportDefinition == null) return NotFound();
			return View(reportDefinition);
		}

		// POST: ReportMail/ReportDefinitions/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var reportDefinition = await _context.ReportDefinitions
				.Include(x => x.ReportFilters)
				.FirstOrDefaultAsync(x => x.ReportDefinitionID == id);

			if (reportDefinition != null)
			{
				if (reportDefinition.ReportFilters?.Any() == true)
					_context.ReportFilters.RemoveRange(reportDefinition.ReportFilters);

				_context.ReportDefinitions.Remove(reportDefinition);
				await _context.SaveChangesAsync();
			}
			return RedirectToAction("Index", "Reports", new { area = "ReportMail" });
		}

		// 供主頁下拉載入自訂「折線圖」報表所需參數：Category + BaseKind + Filters（只含 ValueJson）
		[HttpGet]
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Authorize(Policy = "ReportMail.Reports.Query")]
        public async Task<IActionResult> DefinitionPayload(int id)
		{
			var def = await _context.ReportDefinitions
				.AsNoTracking()
				.Include(d => d.ReportFilters.OrderBy(f => f.OrderIndex))
				.FirstOrDefaultAsync(d => d.ReportDefinitionID == id);
			if (def == null) return NotFound();

			var category = (def.Category ?? "line").Trim().ToLowerInvariant();
			var baseKind = (def.BaseKind ?? "").Trim().ToLowerInvariant();

			var filters = def.ReportFilters.Select(f => new {
				f.FieldName,
				f.Operator,
				f.DataType,
				f.Options,
				f.OrderIndex,
				f.ValueJson
			}).ToList();

			return Json(new
			{
				def.ReportDefinitionID,
				def.ReportName,
				Category = category,   // ★ 標準化後回給前端
				BaseKind = baseKind,   // ★ 標準化後回給前端
				Filters = filters
			});
		}

		private bool ReportDefinitionExists(int id)
			=> _context.ReportDefinitions.Any(e => e.ReportDefinitionID == id);
	}
}