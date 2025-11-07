using BookLoop.Data;
using BookLoop.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers.api
{
    [AllowAnonymous]
    [Route("api/PenatlyRules")]
    [ApiController]
    
    public class PenatlyRulesController : ControllerBase
    {
        private readonly BorrowContext _context;
        public PenatlyRulesController(BorrowContext context) => _context = context;

        [HttpGet("rule")]
        public async Task <IActionResult> GetRules()
        {
            var rules = await  _context.PenaltyRules
            .AsNoTracking() 
            .Where(r=>r.IsActive==true)
            .Select(r => new PenatlyRulesDto
            {
                RuleID = r.RuleID,
                ReasonCode = r.ReasonCode,
                ChargeType = r.ChargeType,
                UnitAmount = r.UnitAmount
            }).ToListAsync();
            return Ok(rules);
        }

    }
}
