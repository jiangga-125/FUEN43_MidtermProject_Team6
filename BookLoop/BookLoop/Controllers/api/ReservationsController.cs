using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Models.Dto;
using BookLoop.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers.api
{
    [ApiController]
    [Route("api/reservations")]
    public class ReservationsController : ControllerBase
    {
        private readonly BorrowContext _context;
        public ReservationsController(BorrowContext context) => _context = context;

        [HttpGet("prepare/{id:int}")]
        public async Task<ActionResult<PrepareReservationResponse>> Prepare(int id)
        {
            var book = await _context.Listings
                .Where(l => l.ListingID == id)
                .Select(l => new { l.ListingID, l.Title, l.Status })
                .SingleOrDefaultAsync();
            if (book is null) return NotFound("查無此書。");
            if (book.Status != 0) return BadRequest("此書目前不可預借。");

            var members = await _context.Members
                .Select(m => new MemberOptionDto { Id = m.MemberID, Name = m.Username })
                .ToListAsync();

            return new PrepareReservationResponse
            {
                ListingId = book.ListingID,
                BookTitle = book.Title,
                DefaultPickupDate = DateTime.Today.AddDays(1),
                DefaultPickupTime = new TimeSpan(17, 30, 0),
                Members = members
            };
        }

        [HttpPost]
        public async Task<ActionResult<CreateReservationResponse>> Create([FromBody] CreateReservationRequest vm)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var listing = await _context.Listings.SingleOrDefaultAsync(l => l.ListingID == vm.ListingId);
            if (listing is null) return NotFound("查無此書。");
            if (listing.Status != 0) return BadRequest("此書目前不可預借。");

            var pickupAt = vm.RequestedPickupDate.Date + vm.RequestedPickupTime;
            var dayCutoff = vm.RequestedPickupDate.Date + new TimeSpan(17, 30, 0);
            var readyAt = DateTime.Today.AddDays(1).AddHours(8);

            using var tx = await _context.Database.BeginTransactionAsync();

            var reservation = new Reservation
            {
                ListingID = vm.ListingId,
                MemberID = vm.MemberId,
                RequestedPickupDate = pickupAt,
                ReservationAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                ReadyAt = readyAt,
                ExpiresAt = dayCutoff,
                Status = (byte)ReservationStatus.Wait,    // 3
                ReservationType = (byte)ReservationType.Normal
            };

            listing.Status = 1; // 保留中

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new CreateReservationResponse
            {
                Ok = true,
                Message = "預借成功，書籍保留中，請於設定時間前往借閱，否則自動取消。",
                ListingId = vm.ListingId,
                NewStatus = 1,
                ReadyAt = readyAt,
                ExpiresAt = dayCutoff,
                ReservationStatus = ReservationStatus.Wait,
                ReservationType = ReservationType.Normal
            };
        }
    }
}
