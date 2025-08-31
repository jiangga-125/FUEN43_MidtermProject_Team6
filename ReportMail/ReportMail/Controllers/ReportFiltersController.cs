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

        // �� �ǳƤU�Կ��ﶵ
        private void PopulateFilterDropdowns(string? selectedDataType = null, string? selectedOperator = null)
        {
            var dataTypes = new[] { "text", "number", "date", "select" };
            var operators = new[] { "=", ">", ">=", "<", "<=", "between", "in", "like" };

            ViewBag.DataTypeOptions = new SelectList(dataTypes, selectedDataType);   // �� �� DataType ��
            ViewBag.OperatorOptions = new SelectList(operators, selectedOperator);   // �� �� Operator ��
        }

        // GET: ReportFilters
        public async Task<IActionResult> Index(int? reportDefinitionId)
        {
            // �� �[�J Include�A�� View �i��� item.ReportDefinition.ReportName
            var query = _context.ReportFilters
                                .Include(rf => rf.ReportDefinition) // ��
                                .AsQueryable();

            if (reportDefinitionId.HasValue)
            {
                // �� Ū�X�ثe����W�١A�ᵹ ViewBag ��������ܡu�ثe����Gxxx�v
                var def = await _context.ReportDefinitions.AsNoTracking()
                                 .FirstOrDefaultAsync(d => d.ReportDefinitionId == reportDefinitionId.Value); // ��
                ViewBag.ReportDefinitionId = reportDefinitionId.Value; // ��
                ViewBag.ReportName = def?.ReportName;                  // ��

                // �� �u��ܸӳ������A�è� OrderIndex �Ƨ�
                query = query.Where(rf => rf.ReportDefinitionId == reportDefinitionId.Value) // ��
                             .OrderBy(rf => rf.OrderIndex);                                   // ��
            }
            else
            {
                // �� �@��M��G���̳���B�A�̱Ƨ�
                query = query.OrderBy(rf => rf.ReportDefinitionId).ThenBy(rf => rf.OrderIndex); // ��
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
            // �� �N ReportDefinitions �ন�U�Ը�ƨӷ��]Value=Id / Text=ReportName�^
            ViewData["ReportDefinitionId"] = new SelectList(
                _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ��
                nameof(ReportDefinition.ReportDefinitionId),                           // ��
                nameof(ReportDefinition.ReportName),                                   // ��
                reportDefinitionId                                                     // �� �w��ثe����
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
            // �� �uô����̪����
            [Bind("ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive")]
            ReportFilter model,
            int? reportDefinitionId /* �� �ΨӦb�x�s��ɦ^�P�@�������L�o�� */)
                {
                    if (ModelState.IsValid)
                    {
                        // �� �ɤW�إ�/��s�ɶ��A�קK DB NOT NULL �X��
                        model.CreatedAt = DateTime.UtcNow; // ��
                        model.UpdatedAt = DateTime.UtcNow; // ��

                        _context.Add(model);
                        await _context.SaveChangesAsync();

                        // �� �x�s��G�Y��e�O�Y�����L�o���A�ɦ^�P���F�_�h�^�@��M��
                        return reportDefinitionId.HasValue
                            ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ��
                            : RedirectToAction(nameof(Index));
                    }

                    // �� ���Ѯɺ����U�Ը�ƻP�����
                    ViewData["ReportDefinitionId"] = new SelectList(
                        _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ��
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

            // �� �ǳƤU��
            ViewData["ReportDefinitionId"] = new SelectList(
                _context.ReportDefinitions.AsNoTracking().OrderBy(d => d.ReportName), // ��
                nameof(ReportDefinition.ReportDefinitionId),
                nameof(ReportDefinition.ReportName),
                entity.ReportDefinitionId
            );
            PopulateFilterDropdowns(entity.DataType, entity.Operator);
            ViewBag.ReportDefinitionId = reportDefinitionId; // �� �^�C��ɫO�d�L�o
            return View(entity);
        }

        // POST: ReportFilters/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            // �� ���uô�����
            [Bind("ReportFilterId,ReportDefinitionId,FieldName,DisplayName,DataType,Operator,DefaultValue,Options,OrderIndex,IsRequired,IsActive")]
            ReportFilter model,
            int? reportDefinitionId)
                {
                    if (id != model.ReportFilterId) return NotFound();

                    if (ModelState.IsValid)
                    {
                        // �� �d�^���ƦA�мg�]�קK��ô�����Q�w�]���л\�^
                        var entity = await _context.ReportFilters.FirstOrDefaultAsync(x => x.ReportFilterId == id); // ��
                        if (entity == null) return NotFound();

                        // �� �ȧ�s�������
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
                        entity.UpdatedAt = DateTime.UtcNow; // ��

                        await _context.SaveChangesAsync();

                        // �� ������O�d�L�o��^
                        return reportDefinitionId.HasValue
                            ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ��
                            : RedirectToAction(nameof(Index));
                    }

                    // �� ���Ѯɺ����U�Ը�ƻP�����
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
                                       .Include(r => r.ReportDefinition) // �� ��� ReportName
                                       .FirstOrDefaultAsync(m => m.ReportFilterId == id);
            if (entity == null) return NotFound();

            ViewBag.ReportDefinitionId = reportDefinitionId; // ��
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

            // �� �R����O�d�L�o��^
            return reportDefinitionId.HasValue
                ? RedirectToAction(nameof(Index), new { reportDefinitionId }) // ��
                : RedirectToAction(nameof(Index));
        }
        private bool ReportFilterExists(int id)
        {
            return _context.ReportFilters.Any(e => e.ReportFilterId == id);
        }
    }
}
