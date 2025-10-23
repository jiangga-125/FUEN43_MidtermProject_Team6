using BookLoop.Models;
using BookLoop.Services;
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
using static BookLoop.ViewModels.BorrowRecordsViewModel;

namespace BookLoop.Areas.Borrows.Controllers
{
    [Area("Borrows")]
    public class BorrowRecordsController : Controller
    {
        private readonly BorrowContext _context;
        private readonly ReservationQueueService _queueService;

        public BorrowRecordsController(BorrowContext context, ReservationQueueService queueService)
        {
            _context = context;
            _queueService = queueService;
        }

        // GET: Borrows/BorrowRecords
        public async Task<IActionResult> Index()
        {
            var items = await _context.BorrowRecords
                .AsNoTracking()
                .Include(b => b.Listing)
                .Include(b => b.Member)
                .Select(b => new BorrowRecordsViewModel
                {
                    ListingID = b.ListingID,
                    RecordID = b.RecordID,
                    BookTitle = b.Listing.Title,
                    MemberID = b.MemberID,//為了罰金補上
                    MemberName = b.Member.Username,
                    BorrowDate = b.BorrowDate,
                    ReturnDate = b.ReturnDate,
                    ReservationID = b.ReservationID,
                    DueDate = b.DueDate,
                    StatusCode = b.StatusCode,
                    CreatedAt = b.CreatedAt,
                    ReturnCondition = (ReturnConditionEnum)b.ReturnCondition,
                    //    // 只用 ReservationID 關聯；Complete 一律回傳 null
                    //ReservationStatus = _context.Reservations
                    //.Where(r => r.ReservationID == b.ReservationID
                    //         && r.Status != (byte)ReservationStatus.Complete)   // ← 過濾掉 Complete
                    //.Select(r => (ReservationStatus?)(byte?)r.Status)
                    //.FirstOrDefault()
                    //// 相關子查詢：同一支 SQL 由資料庫端完成，不拉回記憶體
                    ReservationStatus = _context.Reservations
                        .Where(r =>
                            // 三種匹配情境，任何一個成立就納入候選
                            (b.ReservationID != null && r.ReservationID == b.ReservationID) ||
                            (r.ListingID == b.ListingID && r.MemberID == b.MemberID) ||
                            (r.ListingID == b.ListingID)
                        )
                        // 排序：先依匹配優先度排序，再依“時間新舊”排序
                        .OrderByDescending(r =>
                            b.ReservationID != null && r.ReservationID == b.ReservationID ? 3 :
                            (r.ListingID == b.ListingID && r.MemberID == b.MemberID) ? 2 :
                            (r.ListingID == b.ListingID) ? 1 : 0
                        )
                        .ThenByDescending(r => r.ReservationID)
                        .ThenByDescending(r => r.ReservationAt)
                        .ThenByDescending(r => r.CreatedAt)
                        .Select(r => (ReservationStatus?)(byte?)r.Status)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(items);
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
                .Select(r => new {r.RequestedPickupDate } ) .FirstOrDefaultAsync();

            const int DefaultBorrowDays = 3;//預設借閱天數
            var previewBorrow = DateTime.Now; 
            var previewDue = previewBorrow.AddDays(DefaultBorrowDays);//預覽的應還日期
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



        [HttpGet]
        public async Task<IActionResult> Return(int id)
        {
            var br = await _context.BorrowRecords
            .Include(x => x.Member)
            .Include(x => x.Listing)
            .FirstOrDefaultAsync(x => x.RecordID == id);

            if (br == null)
            {
                TempData["ErrorMessage"] = "找不到借閱紀錄。";
                return RedirectToAction(nameof(Index));
            }

            

            var vm = new BorrowRecordsViewModel
            {
                RecordID = br.RecordID,
                ListingID = br.ListingID,
                MemberName = br.Member.Username,
                MemberID = br.MemberID,
                BorrowDate = br.BorrowDate,
                DueDate = br.DueDate,
                ReturnDate = DateTime.Now,
                StatusCode = br.StatusCode,
                BookTitle = br.Listing.Title,
                ReturnCondition = (ReturnConditionEnum?)br.ReturnCondition
            };

            return PartialView("_ReturnConfirm", vm);

        }

        // POST: /BorrowRecords/Return/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnConfirmed(int id,
            [FromForm(Name = "ReturnCondition")] ReturnConditionEnum? ReturnCondition)
        {
            
            var br = await _context.BorrowRecords.AsTracking()
                .FirstOrDefaultAsync(x => x.RecordID == id);
            if (br == null)
            {
                TempData["ErrorMessage"] = "找不到借閱紀錄。";
                return RedirectToAction(nameof(Index));
            }            
            try
            {
                var today = DateTime.Today;
                br.ReturnDate = today;
                br.StatusCode = (byte)BorrowCondition.Returned;
                br.UpdatedAt = DateTime.Now;
                var overdueDays = Math.Max((today - br.DueDate.Date).Days, 0);
                
                // 規則：逾期歸還 -> 狀態為 Overdue；未逾期歸還 -> 狀態為 Returned
                //以returnat做區分,沒做歸還是null
                br.StatusCode = (byte)(overdueDays > 0 ? BorrowCondition.Overdue
                                               : BorrowCondition.Returned);


                             
                br.ReturnCondition = ReturnCondition.HasValue ? (byte?)(byte)ReturnCondition.Value : null;

                // 保險起見強制標記有變更
                _context.Entry(br).Property(x => x.ReturnCondition).IsModified = true;

                //判斷罰金用
                var promoted = await _queueService.PromoteNextReservationAsync(br.ListingID);

               
                await _context.SaveChangesAsync();


                var msg = overdueDays > 0
               ? $"已完成歸還（逾期 {overdueDays} 天）。"
               : "已完成歸還。";
                TempData["ReturnMessage"] = promoted
                ? $"{msg} 已通知下一位預約者可取書。"
                : $"{msg} 無人預約，書籍已開放借閱。";

            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "資料已被其他人更新，請重新整理後再試。";
            }
            catch (Exception ex)
            {
                // 把真正的錯誤露出來，避免「看起來成功但其實失敗」
                TempData["ErrorMessage"] = "寫入資料庫時發生錯誤：" + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
