using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Contexts;
using BookLoop.Data.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Authorize(Roles = "Admin,Marketing")]
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
            public string? ValueJson { get; set; }    // ߤ@ӷ
            public string? Options { get; set; }
            public int? OrderIndex { get; set; }
            public bool? IsRequired { get; set; }
            public bool? IsActive { get; set; }
        }

        // POST: ReportMail/ReportDefinitions/Create
        //  BaseKind ǤJ BindFɶWݦ۰ʸɡFFiltersJson |i}h ReportFilter]ug ValueJson^
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
            //OҳoзǤơ]hť + pg^Aŭȵw]
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // ]ĳ^sW@߱ҥΡAקKQ Index() Lo
            reportDefinition.IsActive = true;


            _context.Add(reportDefinition);
            await _context.SaveChangesAsync(); //  ReportDefinitionID

            if (!string.IsNullOrWhiteSpace(FiltersJson))
            {
                try
                {
                    var drafts = JsonSerializer.Deserialize<List<ReportFilterDraft>>(FiltersJson) ?? new();
                    int order = 1;
                    foreach (var d in drafts)
                    {
                        // ͵ DisplayName ᴩ]Yeݥ^
                        var display = (d.DisplayName ?? d.FieldName) ?? "";
                        if (string.IsNullOrWhiteSpace(display))
                        {
                            display = (d.FieldName ?? "").ToLowerInvariant() switch
                            {
                                "orderdate" => "϶",
                                "borrowdate" => "϶",
                                "categoryid" => "y",
                                "saleprice" => "楻",
                                "metric" => "",
                                "orderstatus" => "q檬A",
                                "orderamount" => "浧qB",
                                _ => "(RW)"
                            };
                        }

                        _context.ReportFilters.Add(new ReportFilter
                        {
                            ReportDefinitionID = reportDefinition.ReportDefinitionID,
                            FieldName = d.FieldName ?? "",
                            DisplayName = display,
                            DataType = d.DataType ?? "text",
                            Operator = d.Operator ?? "eq",
                            ValueJson = d.ValueJson ?? "{}",   //  ug ValueJson
                            Options = d.Options ?? "{}",
                            OrderIndex = d.OrderIndex ?? order++,
                            IsRequired = d.IsRequired ?? false,
                            IsActive = d.IsActive ?? true,
                            CreatedAt = now,                   //  ݦ۰ʤ
                            UpdatedAt = now
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // ѪR~_Dny{]ni[ ModelState ܡ^
                    Console.WriteLine(ex);
                }
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
        //  BaseKind ǤJ BindAæbxses UpdatedAt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("ReportDefinitionID,ReportName,Category,BaseKind,Description,IsActive,CreatedAt,UpdatedAt")]
            ReportDefinition reportDefinition,
            [FromForm] string? FiltersJson // eݲզn Filter Z(JSON)
        )
        {
            if (id != reportDefinition.ReportDefinitionID) return NotFound();
            if (!ModelState.IsValid) return View(reportDefinition);
            // OҳoзǤơ]hť + pg^Aŭȵw]
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // @߱ҥΡAקKQ Index() Lo
            reportDefinition.IsActive = true;
            reportDefinition.UpdatedAt = DateTime.Now;// ݦ۰ʨs

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {   // 1) s Definition 
                _context.Update(reportDefinition);
                await _context.SaveChangesAsync();

                // 2)屼ªFilters(²BקKﶶǲ)
                var olds = _context.ReportFilters.Where(f => f.ReportDefinitionID == reportDefinition.ReportDefinitionID);
                _context.ReportFilters.RemoveRange(olds);
                await _context.SaveChangesAsync();

                // 3)٭sWQuZѪRv:v[JsFilters
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
                            ValueJson = d.ValueJson ?? "{}",   // su ValueJson
                            Options = d.Options,
                            OrderIndex = order++,
                            IsRequired = d.IsRequired ?? false,   //  d.IsRequired.GetValueOrDefault(false)                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now
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

        // ѥDUԸJۭquuϡvһݰѼơGCategory + BaseKind + Filters]ut ValueJson^
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
                Category = category,   //  зǤƫ^e
                BaseKind = baseKind,   //  зǤƫ^e
                Filters = filters
            });
        }

        private bool ReportDefinitionExists(int id)
            => _context.ReportDefinitions.Any(e => e.ReportDefinitionID == id);
    }
}
