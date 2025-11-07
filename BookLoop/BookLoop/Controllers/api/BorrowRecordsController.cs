using BookLoop.Data;
using BookLoop.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BookLoop.Controllers.api
{
  
    [Route("api/BorrowRecords")]
    [ApiController]
    public class BorrowRecordsController : ControllerBase
    {
        private readonly BorrowContext _db;
        public BorrowRecordsController(BorrowContext db) { _db = db; }

        [HttpGet("records")]
        public async Task<IActionResult> GetBorrowRecords([FromQuery] int memberId = 616)
        {
            var items = await _db.BorrowRecords
            .AsNoTracking()
            .Include(b => b.Listing)
            .Include(b => b.Member)
            .Where(b => b.MemberID == memberId)
            .Select(b => new BorrowDto
            {
                ListingID = b.ListingID,
                RecordID = b.RecordID,
                BookTitle = b.Listing.Title,
                MemberID = b.MemberID,
                MemberName = b.Member.Username,
                BorrowDate = b.BorrowDate,
                ReturnDate = b.ReturnDate,
                DueDate = b.DueDate,
                StatusCode = b.StatusCode,
                ReturnCondition = (byte)b.ReturnCondition,
                // 從你的原本邏輯或 StatusCode 推導（示意）：
                ConditionName = b.StatusCode == 1 ? "借出" :
             b.StatusCode == 0 ? "逾期" :
            b.StatusCode == 2? "歸還" : "-"
            })
            .OrderByDescending(x => x.BorrowDate)
            .ToListAsync();


            return Ok(items);
        }
    }
}

