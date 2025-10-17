using BookLoop.Models;
using BookLoop.ViewModels;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BookLoop.Areas.Borrows.Controllers
{
    [Area("Borrows")]
    public class BorrowRecordsController : Controller
    {
        private readonly BorrowContext _context;

        public BorrowRecordsController(BorrowContext context)
        {
            _context = context;
        }

        // GET: Borrows/BorrowRecords
        public async Task<IActionResult> Index()
        {
            var borrowContext = _context.BorrowRecords.Include(b => b.Listing).Include(b => b.Member).Include(b => b.Reservation);
            return View(await borrowContext.ToListAsync());
        }

        // GET: Borrows/BorrowRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var borrowRecord = await _context.BorrowRecords
                .Include(b => b.Listing)
                .Include(b => b.Member)
                .Include(b => b.Reservation)
                .FirstOrDefaultAsync(m => m.RecordID == id);
            if (borrowRecord == null)
            {
                return NotFound();
            }

            return View(borrowRecord);
        }

        // GET: Borrows/BorrowRecords/Create
        public async Task <IActionResult> Create(int listingId,int memberId,int reservationId)
        {
            

            var borrowlist =await  _context.Listings
                .Where(l=>l.ListingID == listingId)
                .Select(l => new {l.ListingID,l.Title})
                .FirstOrDefaultAsync();
            if (borrowlist == null) 
            {
                return NotFound($"找不到館藏（ListingID={listingId}）。");
            }

            var memberlist= await _context.Members
                .Where(m=>m.MemberID == memberId)//找出借閱人
                .Select(m => new {m.MemberID,m.Username})//挑選需要顯示的欄位
                .FirstOrDefaultAsync();

            if (memberlist == null) { return NotFound($"找不到這個會員（MemberID={memberId}）。"); }

            var reservation =  await _context.Reservations
                .Where(r=>r.ReservationID == reservationId)
                .Select(r => new {r.ReservationAt } ) .FirstOrDefaultAsync();

            const int DefaultBorrowDays = 3;
            var previewBorrow = reservation.ReservationAt; // 或 DateTime.Now
            var previewDue = previewBorrow.AddDays(DefaultBorrowDays);
            var brvm = new BorrowRecordsViewModel
            {
                ListingID = borrowlist.ListingID,
                MemberID = memberlist.MemberID,
                ReservationID = reservationId,
                BookTitle = borrowlist.Title,
                MemberName=memberlist.Username,
                BorrowDate= previewBorrow,
                DueDate = previewDue
            };


            return View(brvm);
        }

        // POST: Borrows/BorrowRecords/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BorrowRecordsViewModel vm)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            // 1) 先把實體載入（可更新）
            var listing = await _context.Listings
                .FirstOrDefaultAsync(l => l.ListingID == vm.ListingID);

            if (listing == null)
                return NotFound($"找不到館藏（ListingID={vm.ListingID}）。");

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberID == vm.MemberID);

            if (member == null)
                return NotFound($"找不到會員（MemberID={vm.MemberID}）。");

            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.ReservationID == vm.ReservationID);
            if (reservation == null)
                return NotFound($"找不到預約（ReservationID={vm.ReservationID}）。");

            var borrowAt = DateTime.Now;                 
            var dueAt = borrowAt.AddDays(3);

            if (!ModelState.IsValid)
            {
                // 只是顯示用，重新補上給畫面
                vm.BookTitle = listing.Title;
                vm.MemberName = member.Username;
                vm.BorrowDate = borrowAt;
                vm.DueDate = dueAt;

                return View(vm);
            }
           

            var borrowRecord = new BorrowRecord
            {
                ListingID = vm.ListingID,
                MemberID = vm.MemberID,
                ReservationID = vm.ReservationID,
                BorrowDate = borrowAt,
                DueDate = dueAt,
                StatusCode = (byte)BorrowRecordsViewModel.BorrowCondition.Borrowed,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReturnDate= null,
                ReturnCondition= null
            };
            _context.BorrowRecords.Add(borrowRecord);

            listing.Status = 2;//借出中
            reservation.Status= 4;//完成借閱

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Success"] = "借閱成功";
            return RedirectToAction("Index","BorrowRecords");
        }

        // GET: Borrows/BorrowRecords/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var borrowRecord = await _context.BorrowRecords.FindAsync(id);
            if (borrowRecord == null)
            {
                return NotFound();
            }
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN", borrowRecord.ListingID);
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", borrowRecord.MemberID);
            ViewData["ReservationID"] = new SelectList(_context.Reservations, "ReservationID", "ReservationID", borrowRecord.ReservationID);
            return View(borrowRecord);
        }

        // POST: Borrows/BorrowRecords/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RecordID,ListingID,MemberID,ReservationID,BorrowDate,ReturnDate,DueDate,StatusCode,CreatedAt,UpdatedAt,ReturnCondition")] BorrowRecord borrowRecord)
        {
            if (id != borrowRecord.RecordID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(borrowRecord);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BorrowRecordExists(borrowRecord.RecordID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN", borrowRecord.ListingID);
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", borrowRecord.MemberID);
            ViewData["ReservationID"] = new SelectList(_context.Reservations, "ReservationID", "ReservationID", borrowRecord.ReservationID);
            return View(borrowRecord);
        }

        // GET: Borrows/BorrowRecords/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var borrowRecord = await _context.BorrowRecords
                .Include(b => b.Listing)
                .Include(b => b.Member)
                .Include(b => b.Reservation)
                .FirstOrDefaultAsync(m => m.RecordID == id);
            if (borrowRecord == null)
            {
                return NotFound();
            }

            return View(borrowRecord);
        }

        // POST: Borrows/BorrowRecords/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var borrowRecord = await _context.BorrowRecords.FindAsync(id);
            if (borrowRecord != null)
            {
                _context.BorrowRecords.Remove(borrowRecord);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BorrowRecordExists(int id)
        {
            return _context.BorrowRecords.Any(e => e.RecordID == id);
        }
    }
}
