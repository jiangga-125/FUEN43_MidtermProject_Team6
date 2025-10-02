using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BorrowSystem.ViewModels;
using static BorrowSystem.ViewModels.BorrowRecordsViewModel;
using BookLoop.Models;
using static BookLoop.Models.BorrowRecord;

using BorrowSystem.Services;

namespace BorrowSystem.Controllers
{
    [Area("Borrow")]
    public class BorrowRecordsController : Controller
    {
        private readonly BorrowSystemContext _context;
        private readonly ReservationQueueService _queueService;
        public BorrowRecordsController(BorrowSystemContext context, ReservationQueueService queueService)
        {
            _context = context;
            _queueService = queueService;
        }

        // GET: BorrowRecords

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
                    MemberName = b.Member.Username,
                    BorrowDate = b.BorrowDate,
                    ReturnDate = b.ReturnDate,


                    ReservationID = b.ReservationID,
                    DueDate = b.DueDate,
                    StatusCode = b.StatusCode,
                    CreatedAt = b.CreatedAt,
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



        // GET: BorrowRecords/Create
        public async Task<IActionResult> Create(int id)
        {
            var info = await _context.Listings
            .Where(l => l.ListingID == id)
            .Select(l => new { l.ListingID, l.Title, l.IsAvailable })
            .FirstOrDefaultAsync();

            if (info == null) return NotFound();

            // 禁止進入 Create
            if (!info.IsAvailable)
            {
                TempData["ErrorMessage"] = "此書已被借閱，僅能預約。";
                return RedirectToAction("Index", "Listings", new { id });
            }

            var listing = await _context.Listings
                .Where(l => l.ListingID == id)
                .Select(l => new BorrowRecordsViewModel
                {
                    ListingID = l.ListingID,
                    BookTitle = l.Title,
                    AuthorIds = l.ListingAuthors.Select(la => la.ListingAuthorID).ToList(),
                    AuthorNames = l.ListingAuthors.Select(la => la.AuthorName).ToList(),
                    BorrowDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(BorrowRecordsViewModel.LoanDays),
                    IsAvailable = l.IsAvailable

                }).AsNoTracking().FirstOrDefaultAsync();

            if (listing == null) return NotFound();

            listing.Members = await _context.Members
            .OrderBy(m => m.Username)
            .Select(m => new SelectListItem
            {
                Value = m.MemberID.ToString(),
                Text = m.Username
            })
            .ToListAsync();

            return View(listing);
        }

        // POST: BorrowRecords/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RecordID,ListingID,MemberID,ReservationID,BorrowDate,ReturnDate,DueDate,StatusCode,CreatedAt,UpdatedAt")] BorrowRecordsViewModel brvw)
        {
            brvw.Members = await _context.Members
            .OrderBy(m => m.Username)
            .Select(m => new SelectListItem
            {
                Value = m.MemberID.ToString(),
                Text = m.Username
            })
            .ToListAsync();

            // 2. 撈出要借的 Listing
            var listing = await _context.Listings
            .Include(l => l.ListingAuthors)
            .FirstOrDefaultAsync(l => l.ListingID == brvw.ListingID);
            if (listing == null)
            {
                FillDisplayFields(brvw, listing);
                ModelState.AddModelError("", "找不到此二手書");
                return View(brvw);
            }
            FillDisplayFields(brvw, listing);

            // 先移除表單舊值，避免覆蓋你設定
            ModelState.Remove(nameof(brvw.BorrowDate));
            ModelState.Remove(nameof(brvw.DueDate));
            ModelState.Remove(nameof(brvw.BookTitle));
            ModelState.Remove(nameof(brvw.AuthorNames));
            // 3. 驗證借閱人
            if (brvw.MemberID == null)
            {
                ModelState.AddModelError(nameof(brvw.MemberID), "請選擇借閱者");
                return View(brvw);
            }

            // 4. 確認是否可借
            if (!listing.IsAvailable)
            {
                ModelState.AddModelError("", "此書目前不可借閱");
                return View(brvw);
            }

            // 5. 確認日期（預設 BorrowDate=今天、DueDate=+14天）
            var today = DateTime.Now;
            var datePicked = (brvw.BorrowDate == default ? today : brvw.BorrowDate);

            // 5.1 借閱日不得早於今天
            if (datePicked.Date < today.Date)
            {
                ModelState.AddModelError(nameof(brvw.BorrowDate), "借閱日不得早於今天");
                return View(brvw);
            }
            var borrowAt = datePicked.Add(today.TimeOfDay); // 與現在相同時分秒
            // 5.2 不信任表單傳來的 DueDate，伺服端統一計算 +14 天        
            var dueAt = borrowAt.AddDays(14);
            brvw.DueDate = dueAt; // 讓回傳 View 時能顯示正確日期

            // 6. 建立借閱紀錄
            var record = new BorrowRecord
            {
                ListingID = brvw.ListingID,
                MemberID = brvw.MemberID.Value,
                BorrowDate = borrowAt,
                DueDate = dueAt,
                StatusCode = (byte)BorrowStatus.Borrowed // 借出
            };

            _context.BorrowRecords.Add(record);

            // 7. 更新 Listing 狀態
            listing.IsAvailable = false;

            // 8. 寫入 DB
            await _context.SaveChangesAsync();

            // 9. 轉回書籍清單
            TempData["SuccessMessage"] = "借閱成功。";
            return RedirectToAction("Index", "BorrowRecords");

        }

        //回填方法:送出資料異常使用
        private static void FillDisplayFields(BorrowRecordsViewModel vm, Listing listing)
        {
            if (listing == null)
            {
                // listing 撈不到就至少給安全值，避免 View 空白
                vm.BookTitle = vm.BookTitle ?? string.Empty;
                vm.AuthorNames = vm.AuthorNames ?? new List<string>();
                return;
            }

            // 正常情況：把 DB 的值補回去
            vm.BookTitle = listing.Title;
            vm.AuthorNames = listing.ListingAuthors?
                .Select(a => a.AuthorName)
                .ToList() ?? new List<string>();

        }

        [HttpGet]
        public async Task<IActionResult> Return(int id)
        {
            var vm = await _context.BorrowRecords
                .Where(x => x.RecordID == id)
                .Select(br => new BorrowRecordsViewModel
                {
                    RecordID = br.RecordID,
                    ListingID = br.ListingID,
                    MemberName = br.Member.Username, // 只取 Username，不會去撈 Account
                    MemberID = br.MemberID,
                    BorrowDate = br.BorrowDate,
                    DueDate = br.DueDate,
                    ReturnDate = DateTime.Today,
                    StatusCode = br.StatusCode,
                    BookTitle = br.Listing.Title
                })
                .FirstOrDefaultAsync();

            if (vm == null)
            {
                TempData["ErrorMessage"] = "找不到借閱紀錄。";
                return RedirectToAction(nameof(Index));
            }

            if ((BorrowCondition)vm.StatusCode == BorrowCondition.Returned)
            {
                TempData["ErrorMessage"] = "此書已經歸還，無法再次操作。";
                return RedirectToAction(nameof(Index));
            }

            return PartialView("_ReturnConfirm", vm);
        }

        // POST: /BorrowRecords/Return/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnConfirmed(int id)
        {
            var br = await _context.BorrowRecords.AsTracking()
                .FirstOrDefaultAsync(x => x.RecordID == id);
            if (br == null)
            {
                TempData["ErrorMessage"] = "找不到借閱紀錄。";
                return RedirectToAction(nameof(Index));
            }
            // 只能從「借出中」歸還
            if ((BorrowCondition)br.StatusCode != BorrowCondition.Borrowed)
            {
                TempData["ErrorMessage"] = "此紀錄不在借出狀態，無法歸還。";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var today = DateTime.Today;
                br.ReturnDate = today;
                br.StatusCode = (byte)BorrowCondition.Returned;
                br.UpdatedAt = DateTime.Now;
                var overdueDays = Math.Max((today - br.DueDate.Date).Days, 0);
                await HandleIsAvailableAsync(br);
                // 規則：逾期歸還 -> 狀態為 Overdue；未逾期歸還 -> 狀態為 Returned
                //以returnat做區分,沒做歸還是null,這是之後期末要做
                br.StatusCode = (byte)(overdueDays > 0 ? BorrowCondition.Overdue
                                               : BorrowCondition.Returned);
               
                var promoted = await _queueService.PromoteNextReservationAsync(br.ListingID);
                //判斷罰金用
                await _context.SaveChangesAsync();
               
              
                var msgPrefix = overdueDays > 0
                ? $"已完成歸還（逾期 {overdueDays} 天）。"
                : "已完成歸還。";
                TempData["ReturnMessage"] = promoted
                ? $"{msgPrefix} 已通知下一位預約者可取書。"
                : $"{msgPrefix} 無人預約，書籍已開放借閱。";
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


        //還書成功後呼叫,讓書變成可借狀態
        private async Task HandleIsAvailableAsync(BorrowRecord br)
        {
            // 將此 Listing 設為可借（如果你有「是否可借」欄位）
            var listing = await _context.Listings.AsTracking().FirstOrDefaultAsync(l => l.ListingID == br.ListingID);
            if (listing != null)
            {
                listing.IsAvailable = true;

            }
        }
    }
}
