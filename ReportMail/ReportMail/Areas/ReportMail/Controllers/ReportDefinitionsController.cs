using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;
using ReportMail.Data.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
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
        public IActionResult Create() => View();

        // 用來承接前端 FiltersJson 的草稿 DTO（只保留 ValueJson）
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

            var now = DateTime.Now;
            reportDefinition.CreatedAt = now;
            reportDefinition.UpdatedAt = now;
            //保證這兩個欄位標準化（去空白 + 小寫），空值給預設
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // （建議）新增一律啟用，避免被 Index() 過濾掉
            reportDefinition.IsActive = true;


            _context.Add(reportDefinition);
            await _context.SaveChangesAsync(); // 先產生 ReportDefinitionID

            if (!string.IsNullOrWhiteSpace(FiltersJson))
            {
                try
                {
                    var drafts = JsonSerializer.Deserialize<List<ReportFilterDraft>>(FiltersJson) ?? new();
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
                            FieldName = d.FieldName ?? "",
                            DisplayName = display,
                            DataType = d.DataType ?? "text",
                            Operator = d.Operator ?? "eq",
                            ValueJson = d.ValueJson ?? "{}",   //  只寫 ValueJson
                            Options = d.Options ?? "{}",
                            OrderIndex = d.OrderIndex ?? order++,
                            IsRequired = d.IsRequired ?? false,
                            IsActive = d.IsActive ?? true,
                            CreatedAt = now,                   //  後端自動化
                            UpdatedAt = now
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // 解析錯誤不阻斷主要流程（必要可加 ModelState 顯示）
                    Console.WriteLine(ex);
                }
            }

            return RedirectToAction(nameof(Index));
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
            // 保證這兩個欄位標準化（去空白 + 小寫），空值給預設
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // 一律啟用，避免被 Index() 過濾掉
            reportDefinition.IsActive = true;
            reportDefinition.UpdatedAt = DateTime.Now;// 後端自動刷新

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
                if (!string.IsNullOrWhiteSpace(FiltersJson))
                {
                    var drafts = System.Text.Json.JsonSerializer.Deserialize<List<ReportFilterDraft>>(FiltersJson) ?? new();
                    var now = DateTime.Now;
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
                            Options = d.Options,
                            OrderIndex = order++,
                            IsRequired = d.IsRequired ?? false,   // 或 d.IsRequired.GetValueOrDefault(false)                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _context.ReportFilters.Add(f);
                    }
                    await _context.SaveChangesAsync();
                }
                await tx.CommitAsync();
                return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
        }

        // 供主頁下拉載入自訂「折線圖」報表所需參數：Category + BaseKind + Filters（只含 ValueJson）
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
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
