using BookLoop.Models;
using BookLoop.ViewModels;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static BookLoop.Models.BorrowRecord;
using static BookLoop.ViewModels.BorrowRecordsViewModel;
using static System.Runtime.InteropServices.JavaScript.JSType;


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
                RequestedPickupDate = DateTime.Today.AddDays(1),       //只帶「明天」日期做預設
                RequestedPickupTime = new TimeSpan(17, 30, 0),         //設定最晚取書時間欄位:17:30

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
            var pickupAt = vm.RequestedPickupDate.Date + vm.RequestedPickupTime;
            var dayCutoff = vm.RequestedPickupDate.Date + new TimeSpan(17, 30, 0);
            var readyat= DateTime.Today.AddDays(1).AddHours(8);
            var reservation = new Reservation
            {
                ListingID = vm.ListingID,
                MemberID = vm.MemberID,
                RequestedPickupDate = pickupAt,// 使用者選擇的取書時間(限制最多今天後3天)
                ReservationAt = DateTime.Now,//預約時間設為現在
                CreatedAt = DateTime.Now,
                ReadyAt = readyat,// 可取書時間設為使用者選擇的時間後,隔天的8點
                ExpiresAt = dayCutoff,// 逾期時間設為當天17:30                
                Status = 3,// 狀態設為等待取書
                ReservationType = 0,// 設預約保留

            };
            listing.Status = 1; // 更新書籍狀態為保留中
                                   
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // 成功後導向（例如導回清單或詳細頁）
            TempData["Success"] = "預借成功,書籍保留中,請於設定時間前往借閱,否則自動取消。";
            return RedirectToAction("Indexfront", "Listings", new { id = vm.ListingID });
        }


        //正式預約用
        // GET: Borrows/Reservations/CreateReservation
        public async Task<IActionResult>CreateReservation(int id)
        {
            var reBook = await _context.Listings.Where(l => l.ListingID == id)
             .Select(l => new { l.ListingID, l.Title })
             .SingleOrDefaultAsync();

            if (reBook == null)
            {
                return NotFound();
            }
            var brRecord = await _context.BorrowRecords
                .Where(b => b.ListingID == id && b.ReturnDate == null)
                .FirstOrDefaultAsync();

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
                ReservationDay = DateTime.Now,
                ReadyDay = await ComputeReadyDayAsync(reBook.ListingID),//根據是否還書判斷可取書日
                                                                       
            };
            vm.ExpiresDay = vm.ReadyDay?.Date.AddDays(3).AddHours(17).AddMinutes(30);
            //逾期日設為可取書後三天的17:30
            return View(vm);
        }
        //預約index加入可取書時間,borrowindex要做修正
        //正式預約用:Post
        // POST: Borrows/Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(ReservationsViewModel vm)
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

            // 判斷自己是否正在借 → 擋
            var hasBorrowed = await _context.BorrowRecords.AnyAsync(b =>
                b.ListingID == listing.ListingID &&
                b.MemberID == vm.MemberID &&
                b.ReturnDate == null);

            if (hasBorrowed)
            {
                TempData["ErrorMessage"] = "您目前正借閱這本書，無需再預約。";
                return RedirectToAction("Indexfront", "Listings", new { id = listing.ListingID });
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
            var brRecord = await _context.BorrowRecords
                .Where(b => b.ListingID == vm.ListingID && b.ReturnDate == null)
                .FirstOrDefaultAsync();

            var reservation = new Reservation
            {
                ListingID = vm.ListingID,
                MemberID = vm.MemberID,
                RequestedPickupDate = null,// 使用者選擇的取書時間
                CreatedAt = DateTime.Now,
                Status = 0,// 狀態設為預約中
                ReservationType = 1,// 設為排隊                
                ReadyAt = await ComputeReadyDayAsync(listing.ListingID),//根據是否還書判斷
                ReservationAt = DateTime.Now//預約時間設為現在
            };

            reservation.ExpiresAt = reservation.ReadyAt?.Date.AddDays(3).AddHours(17).AddMinutes(30);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // 成功後導向（例如導回清單或詳細頁）
            TempData["Success"] = "預借成功,待上個借閱人還書後可取書";
            return RedirectToAction("Index", "Reservations", new { id = vm.ListingID });
        }

        //預約計算readyDay方法
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
                    if (r.ReturnDate is DateTime ret)
                    {    
                        if (r.DueDate is DateTime due && ret.Date > due.Date)
                            return null;
                        //逾期歸還 → null
                        return ret.Date.AddDays(1).AddHours(8); 
                        //否則回傳 ReturnDate 的隔一天（8點）
                        
                    }
                    return null;

                case BorrowStatus.Borrowed:
                    
                    if (r.DueDate is DateTime due2)
                    {                        
                        if (DateTime.Now > due2) return null;
                        return due2.Date.AddDays(1).AddHours(8); 
                        //若 現在時間已超過到期日（逾期未還），不給「可借日」→ null。
                        //否則回傳 到期後隔一天8點
                    }
                    return null;

                case BorrowStatus.Overdue://如果逾期,可取書時間設為null
                default:
                    return null;
            }
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
     
    }
}
