using BookLoop.Models;
using BookLoop.ViewModels;
using DocumentFormat.OpenXml.Bibliography;
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
    public class ReservationsController : Controller
    {
        private readonly BorrowContext _context;

        public ReservationsController(BorrowContext context)
        {
            _context = context;
        }

        // GET: Borrows/Reservations
        public async Task<IActionResult> Index()
        {
            
            var rows = await _context.Reservations.AsNoTracking().Select(r => new ReservationsViewModel
            {
                ReservationID = r.ReservationID,
                ListingID = r.ListingID,
                BookTitle = r.Listing.Title,
                MemberID=r.MemberID,
                MemberName = r.Member.Username,
                ExpiresDay = r.ExpiresAt,                
                ReservationStatus = (ReservationStatus)r.Status,
                ReservationType= (ReservationType)r.ReservationType,

            }).ToListAsync();
            return View(rows);


            
        }

        [HttpGet]
        public async Task<IActionResult> BeforeReservation(int id)
        {
            var reBook = await _context.Listings.Where(l => l.ListingID == id)
             .Select(l => new { l.ListingID, l.Title})
             .SingleOrDefaultAsync();

            if (reBook == null)
            {
                return NotFound();
            }
            

            var members = await _context.Members
           .Select(m => new SelectListItem
           {
               Value = m.MemberID.ToString(),
               Text = m.Username
           })
           .ToListAsync();

            var vm = new ReservationsViewModel
            {
                ListingID = reBook.ListingID,
                BookTitle = reBook.Title,
                Members = members,
                RequestedPickupDate = DateTime.Now,
                
            };           
            return View(vm);
        }

        //預約借書建立
        [HttpPost]
        public async Task<IActionResult> BeforeReservation(ReservationsViewModel vm)
        {
            
            var listing = await _context.Listings
                .SingleOrDefaultAsync(l => l.ListingID == vm.ListingID);

            if (listing == null)
            {
                return NotFound();
            }

            // 自訂驗證：是否選擇會員
            if (vm.MemberID <= 0)
            {
                ModelState.AddModelError(nameof(vm.MemberID), "請選擇借閱人。");
            }

            // 自訂驗證：取書日期（可依需求改成必須 ≥ 今天）
            if (vm.RequestedPickupDate == default)
            {
                ModelState.AddModelError(nameof(vm.RequestedPickupDate), "請輸入取書日期。");
            }

            if (!ModelState.IsValid)
            {
                // 失敗要補回下拉選單與標題
                vm.Members = await _context.Members
                    .Select(m => new SelectListItem
                    {
                        Value = m.MemberID.ToString(),
                        Text = m.Username
                    })
                    .ToListAsync();
                vm.BookTitle = listing.Title;
                return View(vm);
            }
            var now = DateTime.Now; 
            var requested = vm.RequestedPickupDate.Value.Date.Add(now.TimeOfDay);
            
            var reservation = new Reservation
            {
                ListingID = vm.ListingID,
                MemberID = vm.MemberID,
                RequestedPickupDate = requested,
                CreatedAt = DateTime.Now,
                Status = 3,// 狀態設為等待取書
                ReservationType = 0,// 設預約保留
                ExpiresAt = requested,
                ReadyAt = requested,
                ReservationAt = requested
            };
            listing.Status = 1; // 更新書籍狀態為保留中
                                   
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // 成功後導向（例如導回清單或詳細頁）
            TempData["Success"] = "預借成功,書籍保留中,請於設定時間前往借閱,否則自動取消。";
            return RedirectToAction("Indexfront", "Listings", new { id = vm.ListingID });
        }



        // GET: Borrows/Reservations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations
                .Include(r => r.Listing)
                .Include(r => r.Member)
                .FirstOrDefaultAsync(m => m.ReservationID == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // GET: Borrows/Reservations/Create
        public IActionResult Create()
        {
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN");
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username");
            return View();
        }

        // POST: Borrows/Reservations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReservationID,ListingID,MemberID,ReservationAt,ExpiresAt,ReadyAt,Status,CreatedAt,ReservationType,RequestedPickupDate")] Reservation reservation)
        {
            if (ModelState.IsValid)
            {
                _context.Add(reservation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN", reservation.ListingID);
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", reservation.MemberID);
            return View(reservation);
        }

        // GET: Borrows/Reservations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN", reservation.ListingID);
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", reservation.MemberID);
            return View(reservation);
        }

        // POST: Borrows/Reservations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReservationID,ListingID,MemberID,ReservationAt,ExpiresAt,ReadyAt,Status,CreatedAt,ReservationType,RequestedPickupDate")] Reservation reservation)
        {
            if (id != reservation.ReservationID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservationExists(reservation.ReservationID))
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
            ViewData["ListingID"] = new SelectList(_context.Listings, "ListingID", "ISBN", reservation.ListingID);
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", reservation.MemberID);
            return View(reservation);
        }

        // GET: Borrows/Reservations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations
                .Include(r => r.Listing)
                .Include(r => r.Member)
                .FirstOrDefaultAsync(m => m.ReservationID == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // POST: Borrows/Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation != null)
            {
                _context.Reservations.Remove(reservation);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservations.Any(e => e.ReservationID == id);
        }
    }
}
