using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BorrowSystem.Services;
using BorrowSystem.ViewModels;
using BookLoop.BorrowSystem.Models;
using static BookLoop.BorrowSystem.Models.BorrowRecord;
namespace BorrowSystem.Controllers
{
    [Area("Borrow")]
    public class ReservationsController : Controller
    {
        private readonly BorrowSystemContext _context;
        private readonly ReservationQueueService _queueService;

        public ReservationsController(BorrowSystemContext context, ReservationQueueService queueService)
        {
            _context = context;
            _queueService = queueService;
        }

        // GET: Reservations
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            //Include(r => r.Listing).Include(r => r.Member);
            var rows = await _context.Reservations.AsNoTracking().Select(r => new ReservationsViewmodel
            {
                ReservationID = r.ReservationID,
                BookTitle = r.Listing.Title,
                MemberName=r.Member.Username,
                ReservationDay=r.ReservationAt,
                ExpiresDay=r.ExpiresAt,
                ReadyDay=r.ReadyAt,
                ReservationStatus= (ReservationStatus)r.Status

            }).ToListAsync();
            return View(rows);
        }
        
        // GET: Reservations/Create
        public async Task<IActionResult> Create(int id)
        {
            var listing= await _context.Listings.Where(l => l.ListingID == id)
            .Select(l => new { l.ListingID, l.Title, l.IsAvailable })
            .SingleOrDefaultAsync();
            if (listing == null)
            {
                TempData["ErrorMessage"] = "找不到這本書。";
                return RedirectToAction("Index", "BorrowRecords");
            }
            if (listing.IsAvailable == true)
            {
                TempData["ErrorMessage"] = "此書尚未借閱，不用預約。";
                return RedirectToAction("Index", "BorrowRecords", new { id });
            }
            //只要被人預約就不能再預約
            var now = DateTime.Now;
            var hasActiveReservation = await _context.Reservations.AnyAsync(r =>
                r.ListingID == listing.ListingID &&
                (r.Status == (byte)ReservationStatus.Reserved ||
                 r.Status == (byte)ReservationStatus.Wait) &&
                (r.ExpiresAt == null || r.ExpiresAt > now));

            if (hasActiveReservation)
            {
                TempData["ErrorMessage"] = "此書已被預約中，暫時無法再預約。";
                return RedirectToAction("Index", "BorrowRecords");
            }


            var members = await _context.Members
            .Select(m => new SelectListItem
            {
            Value = m.MemberID.ToString(),
            Text = m.Username
             })
            .ToListAsync();
                
        var ReservationsVM = new ReservationsViewmodel
            {
                
                ListingID = listing.ListingID,
                BookTitle = listing.Title,
                Members = members,
                ReservationDay = DateTime.Now,
                ReadyDay = await ComputeReadyDayAsync(listing.ListingID),
                              
        };
            ReservationsVM.ExpiresDay = ReservationsVM.ReadyDay?.AddDays(3);
            return View(ReservationsVM);
        }

        //計算 ReadyDay 的方法      
        private async Task<DateTime?> ComputeReadyDayAsync(int listingId)
        {
            var r = await _context.BorrowRecords
        .Where(b => b.ListingID == listingId)
        .OrderByDescending(b => b.BorrowDate)
        .Select(b => new { b.DueDate, b.ReturnDate, b.StatusCode })
        .FirstOrDefaultAsync();

            if (r is null) return null;

            var status = (BorrowStatus)r.StatusCode;

            switch (status)
            {
                case BorrowStatus.Returned:
                    // ReturnDate 可能為 null → 先判空
                    if (r.ReturnDate is DateTime ret)
                    {
                        // 若你要擋逾期歸還：DueDate 也可能為 null，保護性判斷
                        if (r.DueDate is DateTime due && ret.Date > due.Date)
                            return null;

                        return ret.AddDays(1); // 不固定時間，保留同分同秒
                    }
                    return null;

                case BorrowStatus.Borrowed:
                    // DueDate 可能為 null → 先判空
                    if (r.DueDate is DateTime due2)
                    {
                        // 已過期（逾期）就不給
                        if (DateTime.Now > due2) return null;
                        return due2.AddDays(1); // 不固定時間，保留同分同秒
                    }
                    return null;

                case BorrowStatus.Overdue:
                default:
                    return null;
            }
        }


        // POST: Reservations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReservationsViewmodel revm)
        {
            ModelState.Remove(nameof(ReservationsViewmodel.ReservationDay));
            ModelState.Remove(nameof(ReservationsViewmodel.ExpiresDay));
            ModelState.Remove(nameof(ReservationsViewmodel.ReadyDay));
            //確認 Listing 存在與狀態
            var listing = await _context.Listings
                .Where(l => l.ListingID == revm.ListingID)
                .Select(l => new { l.ListingID, l.Title, l.IsAvailable })
                .SingleOrDefaultAsync();

            if (listing == null)
            {
                TempData["ErrorMessage"] = "找不到這本書。";
                return RedirectToAction("Index", "BorrowRecords");
            }
            if (listing.IsAvailable == true)
            {
                TempData["ErrorMessage"] = "此書尚未借閱，不用預約。";
                return RedirectToAction("Index", "BorrowRecords", new { id = listing.ListingID });
            }

            // 取可取書時間 & 逾期（測試：30 秒；正式：3 天）
            //var nowUtc = DateTime.UtcNow;
            //var readyDay = await ComputeReadyDayAsync(listing.ListingID); // 確認這裡回傳的是 UTC
            //revm.ReadyDay = readyDay ?? nowUtc;                 // 若可能為 null，退回 nowUtc
            //revm.ExpiresDay = (readyDay ?? nowUtc).AddSeconds(30); // 測試用。正式改 AddDays(3)

            
            var readyDay = await ComputeReadyDayAsync(listing.ListingID);
            revm.ReadyDay = readyDay;
            revm.ExpiresDay = readyDay?.AddDays(3);

            if (!ModelState.IsValid)
            {
                // 重新補齊畫面需要的資料（下拉選單、標題）
                revm.BookTitle = listing.Title;
                revm.Members = await _context.Members
                    .Select(m => new SelectListItem
                    {
                        Value = m.MemberID.ToString(),
                        Text = m.Username
                    })
                    .ToListAsync();

                return View(revm);
            }
            // 自己是否正在借 → 擋
            var hasBorrowed = await _context.BorrowRecords.AnyAsync(b =>
                b.ListingID == listing.ListingID &&
                b.MemberID == revm.MemberID &&
                b.ReturnDate == null);

            if (hasBorrowed)
            {
                TempData["ErrorMessage"] = "您目前正借閱這本書，無需再預約。";
                return RedirectToAction("Create", "Reservations", new { id = listing.ListingID });
            }

            // 合併檢查：此書是否已有進行中的預約
            var now = DateTime.Now;
            //var now = DateTime.UtcNow;測試自動逾期用
            var activeMemberIds = await _context.Reservations
                .Where(r => r.ListingID == listing.ListingID &&
                            (r.Status == (byte)ReservationStatus.Reserved ||
                             r.Status == (byte)ReservationStatus.Wait) &&
                            (r.ExpiresAt == null || r.ExpiresAt > now))
                .Select(r => r.MemberID)
                .ToListAsync();

            if (activeMemberIds.Any())
            {
                if (activeMemberIds.Contains(revm.MemberID))
                {
                    TempData["ErrorMessage"] = "您已對此書有進行中的預約。"; 
                }
                else
                {
                    TempData["ErrorMessage"] = "此書已被其他使用者預約中，暫時無法再預約。"; 
                }
                return RedirectToAction("Create", "Reservations", new { id = listing.ListingID });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var entity = new Reservation
                { // 建立資料庫實體，預設狀態為 Reserved
                    ListingID = listing.ListingID,
                    MemberID = revm.MemberID,
                    //ReservationID = revm.ReservationID,
                    ReservationAt = revm.ReservationDay,
                    //ReservationAt = nowUtc,//測試用
                    ReadyAt = revm.ReadyDay,
                    ExpiresAt = revm.ExpiresDay,
                    Status = (byte)ReservationStatus.Reserved,
                    CreatedAt = DateTime.Now
                };
                _context.Reservations.Add(entity);
                await _context.SaveChangesAsync();

                //找到這本書「目前尚未歸還」的那一筆借閱紀錄（理論上只有一筆）
                var activeBorrow = await _context.BorrowRecords
                .Where(b => b.ListingID == listing.ListingID && b.ReturnDate == null)
                .OrderByDescending(b => b.BorrowDate) // 防守性處理
                .FirstOrDefaultAsync();

                // 若該借閱紀錄尚未綁 Reservation，回填剛產生的 ReservationID
                if (activeBorrow != null && activeBorrow.ReservationID == null)
                {
                    activeBorrow.ReservationID = entity.ReservationID;
                    activeBorrow.UpdatedAt = now;
                    _context.BorrowRecords.Update(activeBorrow);
                    await _context.SaveChangesAsync();
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            TempData["SuccessMessage"] = "預約建立完成！";
            return RedirectToAction("Index", "Reservations");
        }

        //手動取消預約
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationID == id);

                if (reservation == null)
                {
                    TempData["ErrorMessage"] = "找不到這筆預約。";
                    return RedirectToAction("Index", "Reservations");
                }

                // 僅能取消「預約中」
                if (reservation.Status != (byte)ReservationStatus.Reserved)
                {
                    TempData["ErrorMessage"] = "書籍在預約中才能做取消。";
                    return RedirectToAction("Index", "Reservations");
                }
                reservation.Status = (byte)ReservationStatus.Cancelled;
                reservation.ExpiresAt = null;

                // 找到目前未歸還且關聯此預約的借閱紀錄，清除關聯
                var activeBorrow = await _context.BorrowRecords
            .Where(b =>
                b.ListingID == reservation.ListingID &&
                b.ReturnDate == null &&
                b.ReservationID == reservation.ReservationID)
            .FirstOrDefaultAsync();

                if (activeBorrow != null)
                {
                    activeBorrow.ReservationID = null;
                    activeBorrow.UpdatedAt = DateTime.Now;
                    _context.BorrowRecords.Update(activeBorrow);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = "預約已取消。";
                return RedirectToAction("Index", "Reservations");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
            // 從「等待取書清單」按鈕（用 reservationId）
            [HttpPost]
        [ValidateAntiForgeryToken]
        // [Authorize(Roles="Admin")]
        public async Task<IActionResult> AdminBorrowFromReservation(int reservationId, int? days)
        {
            var result = await _queueService.AdminBorrowFromWaitReservationAsync(reservationId, days);
            if (!result.ok)
                TempData["ErrorMessage"] = result.error ?? "辦理借出失敗。";
            else
                TempData["SuccessMessage"] = "已完成借出（預約者）。";
            return RedirectToAction(nameof(Index));
        }
    }
}
