using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace ReportMail.Areas.ReportMail.Controllers
{
    public class ReportDefinitionsController : ReportMailAreaController
    {
        private readonly ReportMailDbContext _context;

        public ReportDefinitionsController(ReportMailDbContext context)
        {
            _context = context;
        }

        // GET: ReportMail/ReportDefinitions
        public async Task<IActionResult> Index()
        {
            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();

            if ((await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewAny")).Succeeded)
            {
                var list = await _context.ReportDefinitions.AsNoTracking()
                             .OrderByDescending(x => x.UpdatedAt).ToListAsync();
                return View(list);
            }

            if ((await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewOwn")).Succeeded)
            {
                var myId = CurrentUserIdOrNull();
                if (myId is null) return Forbid();
                var list = await _context.ReportDefinitions.AsNoTracking()
                             .Where(x => x.OwnerUserID == myId)
                             .OrderByDescending(x => x.UpdatedAt).ToListAsync();
                return View(list);
            }

            return Forbid();
        }

        // GET: ReportMail/ReportDefinitions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var def = await _context.ReportDefinitions
                       .Include(r => r.ReportFilters.OrderBy(f => f.OrderIndex))
                       .FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
            if (def == null) return NotFound();

            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            if ((await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewAny")).Succeeded)
                return View(def);

            if ((await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.ViewOwn")).Succeeded)
            {
                var myId = CurrentUserIdOrNull();
                if (myId is null) return Forbid();
                if (def.OwnerUserID == myId) return View(def);
            }

            return Forbid();
        }

        // GET: ReportMail/ReportDefinitions/Create
        [Authorize(Policy = "ReportMail.Reports.Def.Create")]

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
        [Authorize(Policy = "ReportMail.Reports.Def.Create")]

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

            var uid = CurrentUserIdOrNull();
            if (uid is null) return Forbid();
            reportDefinition.OwnerUserID = uid;

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
                            // ★ 新增書商相關篩選的友善名稱
                            "supplierid" => "書商/供應商",
                            "publisherid" => "出版商",
                            // ★ 結束新增
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
            var def = await _context.ReportDefinitions
                                    .Include(d => d.ReportFilters.OrderBy(f => f.OrderIndex))
                                    .FirstOrDefaultAsync(x => x.ReportDefinitionID == id);
            if (def == null) return NotFound();

            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAny = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.EditAny")).Succeeded;
            var canOwn = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.EditOwn")).Succeeded;
            var myId = CurrentUserIdOrNull();
            if (myId is null) return Forbid();// 非 Admin 且無法識別 UserID -> Forbid

            if (!(canAny || (canOwn && def.OwnerUserID == myId))) return Forbid();
            // 將 Filters 序列化傳給 View
            ViewBag.FiltersJson = JsonSerializer.Serialize(def.ReportFilters.Select(f => new ReportFilterDraft
            {
                FieldName = f.FieldName,
                DisplayName = f.DisplayName,
                DataType = f.DataType,
                Operator = f.Operator,
                ValueJson = f.ValueJson, // 只傳 ValueJson
                Options = f.Options,
                OrderIndex = f.OrderIndex,
                IsRequired = f.IsRequired,
                IsActive = f.IsActive
            }));
            return View(def);
        }

        // POST: ReportMail/ReportDefinitions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            //  Bind 屬性接收表單傳來的值
            [Bind("ReportDefinitionID,ReportName,Category,BaseKind,Description,IsActive,CreatedAt,UpdatedAt")]
            ReportDefinition inputDefinition, 
            [FromForm] string? FiltersJson)
        {
            if (id != inputDefinition.ReportDefinitionID) return NotFound();

            // 先解析 FiltersJson，如果格式錯誤，提前返回
            if (!TryParseFilterDrafts(FiltersJson, out var drafts))
            {
                // 需要重新載入原始資料和 FiltersJson 以便 View 能正確顯示
                var originalDef = await _context.ReportDefinitions
                                      .Include(d => d.ReportFilters.OrderBy(f => f.OrderIndex))
                                      .AsNoTracking() // 只需要讀取
                                      .FirstOrDefaultAsync(x => x.ReportDefinitionID == id);
                if (originalDef == null) return NotFound(); // 理論上不會發生
                ViewBag.FiltersJson = FiltersJson; // 保留使用者輸入的錯誤 Json 或原 Json
                ModelState.AddModelError("FiltersJson", "篩選條件格式不正確。"); // 加入明確錯誤訊息
                return View(originalDef); // 返回 View 顯示錯誤
            }


            // 授權：EditAny / EditOwn（以資料庫現值 owner 為準）
            // 查詢用於 *更新* 的實體 (entity)，這次 *不要* 用 AsNoTracking() 
            var entity = await _context.ReportDefinitions
                            .Include(x => x.ReportFilters) // 同時載入舊的 Filters 以便刪除
                            .FirstOrDefaultAsync(x => x.ReportDefinitionID == id);
            if (entity == null) return NotFound();

            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAny = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.EditAny")).Succeeded;
            var canOwn = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.EditOwn")).Succeeded;
            var myId = CurrentUserIdOrNull();
            if (myId is null && !canAny) return Forbid();
            if (!(canAny || (canOwn && entity.OwnerUserID == myId))) return Forbid(); // ★ 使用 entity 判斷擁有者

            // 檢查 ModelState (在查詢 entity 之後，以便錯誤時能返回 View)
            if (!ModelState.IsValid)
            {
                ViewBag.FiltersJson = FiltersJson; // 保留使用者輸入的 FiltersJson
                return View(entity); // 返回 View 顯示 entity 的當前狀態和錯誤
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                //更新從資料庫讀取的 entity 
                entity.ReportName = inputDefinition.ReportName;
                entity.Category = (inputDefinition.Category ?? "line").Trim().ToLowerInvariant();
                entity.BaseKind = (inputDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();
                entity.Description = inputDefinition.Description;
                entity.IsActive = inputDefinition.IsActive; //從表單接收 IsActive 的值
                entity.UpdatedAt = DateTime.UtcNow; // 更新時間戳
                //  OwnerUserID 不更新，保持 entity 從資料庫讀取到的原始值 


                // 2) 砍掉舊的Filters (使用 entity.ReportFilters)
                if (entity.ReportFilters?.Any() == true)
                {
                    _context.ReportFilters.RemoveRange(entity.ReportFilters);

                }

                // 3) 還原新增時的「草稿解析」:逐筆加入新的Filters
                if (drafts.Count > 0)
                {
                    int order = 1;
                    foreach (var d in drafts)
                    {
                        // 友善名稱邏輯 (與 Create 保持一致)
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
                                "supplierid" => "書商/供應商",
                                "publisherid" => "出版商",
                                _ => "(未命名)"
                            };
                        }

                        if (string.IsNullOrWhiteSpace(d.FieldName)) continue;
                        var f = new ReportFilter
                        {
                            ReportDefinitionID = entity.ReportDefinitionID, // 使用 entity 的 ID
                            FieldName = d.FieldName!.Trim(),
                            DisplayName = display, 
                            DataType = (d.DataType ?? "text").Trim().ToLowerInvariant(),
                            Operator = (d.Operator ?? "eq").Trim().ToLowerInvariant(),
                            ValueJson = d.ValueJson ?? "{}",
                            Options = d.Options ?? "{}",
                            OrderIndex = d.OrderIndex ?? order++, // 如果前端沒給 OrderIndex，才自動遞增
                            IsRequired = d.IsRequired ?? false,
                            IsActive = d.IsActive ?? true // Filter 預設 Active
                        };
                        _context.ReportFilters.Add(f);
                    }
                }

                // 一次性儲存所有變更
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return RedirectToAction("Index", "Reports", new { area = "ReportMail" });
            }
            catch (DbUpdateConcurrencyException) // 並發衝突
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "此筆資料已被其他人修改，請重新載入後再試。");
                ViewBag.FiltersJson = FiltersJson; // 保留 FiltersJson
                return View(entity); // 返回 View 顯示 entity 的當前狀態
            }
            catch (DbUpdateException ex) // 其他資料庫更新錯誤 (例如唯一約束)
            {
                await tx.RollbackAsync();
                if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
                {
                    ModelState.AddModelError(string.Empty, "儲存失敗，報表名稱或其他唯一欄位已存在。");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "儲存失敗，請檢查資料格式或聯繫管理員。");
                }
                ViewBag.FiltersJson = FiltersJson; // 保留 FiltersJson
                return View(entity); // 返回 View 顯示 entity 的當前狀態
            }
            catch // 其他未預期錯誤
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "發生未預期的錯誤，請稍後再試。");
                ViewBag.FiltersJson = FiltersJson; // 保留 FiltersJson
                return View(entity); // 返回 View 顯示 entity 的當前狀態
            }
        }

        // GET: ReportMail/ReportDefinitions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var def = await _context.ReportDefinitions.FirstOrDefaultAsync(m => m.ReportDefinitionID == id);
            if (def == null) return NotFound();

            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAny = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.DeleteAny")).Succeeded;
            var canOwn = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.DeleteOwn")).Succeeded;
            var myId = CurrentUserIdOrNull();
            if (myId is null) return Forbid();

            if (!(canAny || (canOwn && def.OwnerUserID == myId))) return Forbid();
            return View(def);
        }

        // POST: ReportMail/ReportDefinitions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var def = await _context.ReportDefinitions
                 .Include(x => x.ReportFilters)
                 .FirstOrDefaultAsync(x => x.ReportDefinitionID == id);
            if (def == null) return NotFound();

            // 授權：DeleteAny / DeleteOwn
            var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var canAny = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.DeleteAny")).Succeeded;
            var canOwn = (await auth.AuthorizeAsync(User, "ReportMail.Reports.Def.DeleteOwn")).Succeeded;
            var myId = CurrentUserIdOrNull();
            if (myId is null) return Forbid();
            if (!(canAny || (canOwn && def.OwnerUserID == myId))) return Forbid();

            if (def.ReportFilters?.Any() == true)
                _context.ReportFilters.RemoveRange(def.ReportFilters);

            _context.ReportDefinitions.Remove(def);
            await _context.SaveChangesAsync();

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

        private int? CurrentUserIdOrNull()
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s, out var id) ? id : (int?)null;
        }
    }
}