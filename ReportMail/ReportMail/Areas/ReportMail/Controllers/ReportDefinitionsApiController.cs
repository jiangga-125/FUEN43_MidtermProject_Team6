using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportMail.Data.Contexts;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Route("ReportMail/[controller]/[action]")]
    public class ReportDefinitionsApiController : Controller
    {
        private readonly ReportMailDbContext _db;
        public ReportDefinitionsApiController(ReportMailDbContext db) => _db = db;

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var cat = await _db.ReportDefinitions
                               .AsNoTracking()
                               .Where(x => x.ReportDefinitionID == id)
                               .Select(x => x.Category)
                               .FirstOrDefaultAsync();
            if (cat == null) return NotFound();
            return Json(new { category = cat });
        }
    }
}
