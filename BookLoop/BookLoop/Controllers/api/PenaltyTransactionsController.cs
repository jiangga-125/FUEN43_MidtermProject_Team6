using BookLoop.Data;
using BookLoop.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers.api
{
    [AllowAnonymous]
    [Route("api/PenaltyTransactions")]
    [ApiController]
    public class PenaltyTransactionsController : ControllerBase
    {
        private readonly BorrowContext _context;
        public PenaltyTransactionsController(BorrowContext context) => _context = context;

        [HttpGet("penalties")]
        public async Task<IActionResult> GetPenalties([FromQuery] int memberId = 616)
        {
            
        var items = await _context.PenaltyTransactions
            .AsNoTracking()
            .Include(p => p.Member)
            .Include(x => x.Rule)
            .Include(p => p.Record)
            .ThenInclude(r => r.Listing)
            .Where(p => p.MemberID == memberId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PenaltyDto
            {
                PenaltyID = p.PenaltyID,
                MemberName = p.Member.Username,
                BookTitle = p.Record.Listing.Title,
                Amount = p.Rule.UnitAmount,
                Reason = p.Rule.ReasonCode,
                CreatedAt = p.CreatedAt,
                totalMoney = p.Quantity * p.Rule.UnitAmount,//計算總價
                PaidAt = p.PaidAt
            })
            .ToListAsync();


            return Ok(items);
        }
    }
}
