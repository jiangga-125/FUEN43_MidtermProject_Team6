using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReportMail.Models;

namespace ReportMail.Controllers
{
    public class ReportFiltersController : Controller
    {
        private readonly ReportMailDbContext _context;

        public ReportFiltersController(ReportMailDbContext context)
        {
            _context = context;
        }

        // ★ 準備下拉選單選項
        private void PopulateFilterDropdowns(string? selectedDataType = null, string? selectedOperator = null)
        {
            var dataTypes = new[] { "text", "number", "date", "select" };
            var operators = new[] { "=", ">", ">=", "<", "<=", "between", "in", "like" };

            ViewBag.DataTypeOptions = new SelectList(dataTypes, selectedDataType);   // ★ 給 DataType 用
            ViewBag.OperatorOptions = new SelectList(operators, selectedOperator);   // ★ 給 Operator 用
        }

        // GET: ReportFilters
        public async Task<IActionResult> Index(int? reportDefinitionId)
        {
            // ★ 加入 Include，讓 View 可顯示 item.ReportDefinition.ReportName
            var query = _context.ReportFilters
                                .Include(rf => rf.ReportDefinition) // ★
                                .AsQueryable();

            if (reportDefinitionId.HasValue)
            {
                // ★ 讀出目前報表名稱，丟給 ViewBag 讓頁面顯示「目前報表：xxx」
                var def = await _context.ReportDefinitions.AsNoTracking()
                                 .FirstOrDefaultAsync(d => d.ReportDefinitionId == reportDefinitionId.Value); // ★
                ViewBag.ReportDefinitionId = reportDefinitionId.Value; // ★
                ViewBag.ReportName = def?.ReportName;                  // ★

                // ★ 只顯示該報表的欄位，並依 OrderIndex 排序
                query = query.Where(rf => rf.ReportDefinitionId == reportDefinitionId.Value) // ★
                             .OrderBy(rf => rf.OrderIndex);                                   // ★
            }
            else
            {
                // ★ 一般清單：先依報表、再依排序
                query = query.OrderBy(rf => rf.ReportDefinitionId).ThenBy(rf => rf.OrderIndex); // ★
            }

            return View(await query.ToListAsync());
        }

        // GET: ReportFilters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reportFilter = await _context.ReportFilters
                .Include(r => r.ReportDefinition)
                .FirstOrDefaultAsync(m => m.ReportFilterId == id);
            if (reportFilter == null)
            {
                return NotFound();
            }

            return View(reportFilter);
        }

        // GET: ReportFilters/Create
        public IActionResult Create(int? reportDefinitionId)
        {
            // ★ 將 ReportDefinitions 轉成下拉資料來源（Value=Id / Text=ReportName）
            ViewData["ReportDefinitionId"] = new SelectList(
                _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ★
                nameof(ReportDefinition.ReportDefinitionId),                           // ★
                nameof(ReportDefinition.ReportName),                                   // ★
                reportDefinitionId                                                     // ★ 預選目前報表
            );
            PopulateFilterDropdowns();
            return View();
        }


        // POST: ReportFilters/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            // ★ 只繫結表裡的欄位
            [Bind("ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive")]
            ReportFilter model,
            int? reportDefinitionId /* ★ 用來在儲存後導回同一份報表的過濾頁 */)
                {
                    if (ModelState.IsValid)
                    {
                        // ★ 補上建立/更新時間，避免 DB NOT NULL 出錯
                        model.CreatedAt = DateTime.UtcNow; // ★
                        model.UpdatedAt = DateTime.UtcNow; // ★

                        _context.Add(model);
                        await _context.SaveChangesAsync();

                        // ★ 儲存後：若當前是某報表的過濾頁，導回同頁；否則回一般清單
                        return reportDefinitionId.HasValue
                            ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ★
                            : RedirectToAction(nameof(Index));
                    }

                    // ★ 失敗時維持下拉資料與選取值
                    ViewData["ReportDefinitionId"] = new SelectList(
                        _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ★
                        nameof(ReportDefinition.ReportDefinitionId),
                        nameof(ReportDefinition.ReportName),
                        model.ReportDefinitionId
                    );
                    PopulateFilterDropdowns(model.DataType, model.Operator);
                    return View(model);
                }

        // GET: ReportFilters/Edit/5
        public async Task<IActionResult> Edit(int? id, int? reportDefinitionId)
        {
            if (id == null) return NotFound();
            var entity = await _context.ReportFilters.FindAsync(id);
            if (entity == null) return NotFound();

            // ★ 準備下拉
            ViewData["ReportDefinitionId"] = new SelectList(
                _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ★
                nameof(ReportDefinition.ReportDefinitionId),
                nameof(ReportDefinition.ReportName),
                entity.ReportDefinitionId
            );
            PopulateFilterDropdowns(entity.DataType, entity.Operator);
            ViewBag.ReportDefinitionId = reportDefinitionId; // ★ 回列表時保留過濾
            return View(entity);
        }

        // POST: ReportFilters/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            // ★ 仍只繫結欄位
            [Bind("ReportFilterId,ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive")]
            ReportFilter model,
            int? reportDefinitionId)
                {
                    if (id != model.ReportFilterId) return NotFound();

                    if (ModelState.IsValid)
                    {
                        // ★ 查回原資料再覆寫（避免未繫結欄位被預設值覆蓋）
                        var entity = await _context.ReportFilters.FirstOrDefaultAsync(x => x.ReportFilterId == id); // ★
                        if (entity == null) return NotFound();

                        // ★ 僅更新有的欄位
                        entity.ReportDefinitionId = model.ReportDefinitionId;
                        entity.FieldName = model.FieldName;
                        entity.DisplayName = model.DisplayName;
                        entity.DataType = model.DataType;
                        entity.Operator = model.Operator;
                        entity.DefaultValue = model.DefaultValue;
                        entity.Options = model.Options;
                        entity.OrderIndex = model.OrderIndex;
                        entity.IsRequired = model.IsRequired;
                        entity.IsActive = model.IsActive;
                        entity.UpdatedAt = DateTime.UtcNow; // ★

                        await _context.SaveChangesAsync();

                        // ★ 完成後保留過濾返回
                        return reportDefinitionId.HasValue
                            ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ★
                            : RedirectToAction(nameof(Index));
                    }

                    // ★ 失敗時維持下拉資料與選取值
                    ViewData["ReportDefinitionId"] = new SelectList(
                        _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName),
                        nameof(ReportDefinition.ReportDefinitionId),
                        nameof(ReportDefinition.ReportName),
                        model.ReportDefinitionId
                    );
                    PopulateFilterDropdowns(model.DataType, model.Operator);
                    return View(model);
                }

        // GET: ReportFilters/Delete/5
        public async Task<IActionResult> Delete(int? id, int? reportDefinitionId)
        {
            if (id == null) return NotFound();
            var entity = await _context.ReportFilters
                                       .Include(r => r.ReportDefinition) // ★ 顯示 ReportName
                                       .FirstOrDefaultAsync(m => m.ReportFilterId == id);
            if (entity == null) return NotFound();

            ViewBag.ReportDefinitionId = reportDefinitionId; // ★
            return View(entity);
        }


        // POST: ReportFilters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, int? reportDefinitionId)
        {
            var entity = await _context.ReportFilters.FindAsync(id);
            if (entity != null)
            {
                _context.ReportFilters.Remove(entity);
                await _context.SaveChangesAsync();
            }

            // ★ 刪除後保留過濾返回
            return reportDefinitionId.HasValue
                ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ★
                : RedirectToAction(nameof(Index));
        }
        private bool ReportFilterExists(int id)
        {
            return _context.ReportFilters.Any(e => e.ReportFilterId == id);
        }
    }
}
