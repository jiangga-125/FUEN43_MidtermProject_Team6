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
					model.Category = c; // è®?Create.cshtml ??<select asp-for="Category"> ?è¨­?¸åˆ°å°æ??…ç›®
			}
			return View(model);
		}

		// ?¨ä??¿æ¥?ç«¯ FiltersJson ?„è?ç¨?DTOï¼ˆåªä¿ç? ValueJsonï¼?
		private class ReportFilterDraft
        {
            public string? FieldName { get; set; }
            public string? DisplayName { get; set; }
            public string? DataType { get; set; }
            public string? Operator { get; set; }
            public string? ValueJson { get; set; }    // ?¯ä?ä¾†æ?
            public string? Options { get; set; }
            public int? OrderIndex { get; set; }
            public bool? IsRequired { get; set; }
            public bool? IsActive { get; set; }
        }

        // POST: ReportMail/ReportDefinitions/Create
        // ??BaseKind ç´å…¥ Bindï¼›æ??“æˆ³å¾Œç«¯?ªå?è£œï?FiltersJson ?ƒå??‹ç‚ºå¤šç? ReportFilterï¼ˆåªå¯?ValueJsonï¼?
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
            //ä¿è??™å…©?‹æ?ä½æ?æº–å?ï¼ˆå»ç©ºç™½ + å°å¯«ï¼‰ï?ç©ºå€¼çµ¦?è¨­
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // ï¼ˆå»ºè­°ï??°å?ä¸€å¾‹å??¨ï??¿å?è¢?Index() ?æ¿¾??
            reportDefinition.IsActive = true;


            _context.Add(reportDefinition);
            await _context.SaveChangesAsync(); // ?ˆç”¢??ReportDefinitionID

            if (!string.IsNullOrWhiteSpace(FiltersJson))
            {
                try
                {
                    var drafts = JsonSerializer.Deserialize<List<ReportFilterDraft>>(FiltersJson) ?? new();
                    int order = 1;
                    foreach (var d in drafts)
                    {
                        // ?‹å???DisplayName å¾Œæ´ï¼ˆè‹¥?ç«¯?ªçµ¦ï¼?
                        var display = (d.DisplayName ?? d.FieldName) ?? "";
                        if (string.IsNullOrWhiteSpace(display))
                        {
                            display = (d.FieldName ?? "").ToLowerInvariant() switch
                            {
                                "orderdate" => "?¥æ??€??,
                                "borrowdate" => "?¥æ??€??,
                                "categoryid" => "?¸ç?ç¨®é?",
                                "saleprice" => "?®æœ¬?¹ä?",
                                "metric" => "?‡æ?",
                                "orderstatus" => "è¨‚å–®?€??,
                                "orderamount" => "?®ç?è¨‚å–®?‘é?",
                                _ => "(?ªå‘½??"
                            };
                        }

                        _context.ReportFilters.Add(new ReportFilter
                        {
                            ReportDefinitionID = reportDefinition.ReportDefinitionID,
                            FieldName = d.FieldName ?? "",
                            DisplayName = display,
                            DataType = d.DataType ?? "text",
                            Operator = d.Operator ?? "eq",
                            ValueJson = d.ValueJson ?? "{}",   //  ?ªå¯« ValueJson
                            Options = d.Options ?? "{}",
                            OrderIndex = d.OrderIndex ?? order++,
                            IsRequired = d.IsRequired ?? false,
                            IsActive = d.IsActive ?? true,
                            CreatedAt = now,                   //  å¾Œç«¯?ªå???
                            UpdatedAt = now
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // è§???¯èª¤ä¸é˜»?·ä¸»è¦æ?ç¨‹ï?å¿…è??¯å? ModelState é¡¯ç¤ºï¼?
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
        // ??BaseKind ç´å…¥ Bindï¼Œä¸¦?¨å„²å­˜å??·æ–° UpdatedAt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("ReportDefinitionID,ReportName,Category,BaseKind,Description,IsActive,CreatedAt,UpdatedAt")]
            ReportDefinition reportDefinition,
            [FromForm] string? FiltersJson // ?¥æ”¶?ç«¯çµ„å¥½??Filter ?‰ç¨¿(JSON)
        )
        {
            if (id != reportDefinition.ReportDefinitionID) return NotFound();
            if (!ModelState.IsValid) return View(reportDefinition);
            // ä¿è??™å…©?‹æ?ä½æ?æº–å?ï¼ˆå»ç©ºç™½ + å°å¯«ï¼‰ï?ç©ºå€¼çµ¦?è¨­
            reportDefinition.Category = (reportDefinition.Category ?? "line").Trim().ToLowerInvariant();
            reportDefinition.BaseKind = (reportDefinition.BaseKind ?? "sales").Trim().ToLowerInvariant();

            // ä¸€å¾‹å??¨ï??¿å?è¢?Index() ?æ¿¾??
            reportDefinition.IsActive = true;
            reportDefinition.UpdatedAt = DateTime.Now;// å¾Œç«¯?ªå??·æ–°

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {   // 1) ?ˆæ›´??Definition ?¬é?
                _context.Update(reportDefinition);
                await _context.SaveChangesAsync();

                // 2)?æ??Šç?Filters(?€ç°¡æ??é¿?æ?å°é?åºç•°??
                var olds = _context.ReportFilters.Where(f => f.ReportDefinitionID == reportDefinition.ReportDefinitionID);
                _context.ReportFilters.RemoveRange(olds);
                await _context.SaveChangesAsync();

                // 3)?„å??°å??ç??Œè?ç¨¿è§£?ã€??ç?? å…¥?°ç?Filters
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
                            ValueJson = d.ValueJson ?? "{}",   // ?°ç??ªç”¨ ValueJson
                            Options = d.Options,
                            OrderIndex = order++,
                            IsRequired = d.IsRequired ?? false,   // ??d.IsRequired.GetValueOrDefault(false)                            IsActive = true,
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

        // ä¾›ä¸»?ä??‰è??¥è‡ªè¨‚ã€Œæ?ç·šå??å ±è¡¨æ??€?ƒæ•¸ï¼šCategory + BaseKind + Filtersï¼ˆåª??ValueJsonï¼?
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
                Category = category,   // ??æ¨™æ??–å??çµ¦?ç«¯
                BaseKind = baseKind,   // ??æ¨™æ??–å??çµ¦?ç«¯
                Filters = filters
            });
        }

        private bool ReportDefinitionExists(int id)
            => _context.ReportDefinitions.Any(e => e.ReportDefinitionID == id);
    }
}
