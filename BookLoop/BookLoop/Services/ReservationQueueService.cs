using Microsoft.EntityFrameworkCore;
using BorrowSystem.ViewModels;
using static BorrowSystem.ViewModels.BorrowRecordsViewModel;
using BookLoop.BorrowSystem.Models;
namespace BorrowSystem.Services
{
    //推進預約使用
    public class ReservationQueueService
    {
        private readonly BorrowSystemContext _db;
        private const int DefaultBorrowDays = 14;
        public ReservationQueueService(BorrowSystemContext db) => _db = db;

        /// <summary>
        /// 書籍歸還後：若有人預約，將最早的 Reserved → Wait，並設定 ReadyAt / ExpiresAt(3天)
        /// 若無預約，將書籍標為可借閱 (IsAvailable = true)。
        /// 回傳值：true 表示有推進到「等待取書」；false 表示沒人預約。
        /// </summary>
        public async Task<bool> PromoteNextReservationAsync(int listingId, CancellationToken ct = default)
        {
            // 建議用交易，避免競態
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 1) 鎖定這本書的存貨狀態（可選，避免多人同時處理）
            var listing = await _db.Listings
                .FirstOrDefaultAsync(l => l.ListingID == listingId, ct);
            if (listing == null)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            // 2) 取「最早的有效預約」：只取狀態=Reserved 的、依預約時間排序 (FIFO)
            var next = await _db.Reservations
                .Where(r => r.ListingID == listingId
                         && r.Status == (byte)ReservationStatus.Reserved)
                .OrderBy(r => r.ReservationAt)
                .FirstOrDefaultAsync(ct);

            if (next == null)
            {
                // 沒有人預約 → 書可借
                listing.IsAvailable = true;
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return false;
            }

            // 3) 推進狀態：Reserved → Wait，並寫入可取書時間與逾期日（+3 天）
            // 測試；正式用 AddDays(3)
            //var now = DateTime.UtcNow;
            //next.Status = (byte)ReservationStatus.Wait;
            //next.ReadyAt = now;
            //next.ExpiresAt = now.AddSeconds(30);
            
            var now = DateTime.Now;
            next.Status = (byte)ReservationStatus.Wait;

            //// 設定可取書時間 & 逾期日
            next.ReadyAt = now;
            next.ExpiresAt = now.AddDays(3);

            // 歸還後這本書「有預約者等待取書」，通常標為不可借閱
            listing.IsAvailable = false;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            
            return true;
        }
        //從「Wait 清單」用 reservationId 辦理
        public async Task<(bool ok, string? error)> AdminBorrowFromWaitReservationAsync(
            int reservationId, int? borrowDays = null, CancellationToken ct = default)
        {
            return await BorrowFromWaitCoreAsync(reservationId, borrowDays, ct);
        }


        // 共用核心：把 Wait 預約轉成借閱（只限該預約者）
        private async Task<(bool ok, string? error)> BorrowFromWaitCoreAsync(
            int reservationId, int? borrowDays, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var r = await _db.Reservations
                .Include(x => x.Listing)
                .FirstOrDefaultAsync(x => x.ReservationID == reservationId, ct);

            if (r == null) return (false, "找不到預約。");
            if (r.Status != (byte)ReservationStatus.Wait) return (false, "預約不在等待取書狀態。");

            var now = DateTime.Now;
            //var now = DateTime.UtcNow;測試自動逾期用
            if (r.ExpiresAt != null && r.ExpiresAt < now) return (false, "此預約已逾期失效。");

            // ★★ 同步補齊「上一筆借閱（會員A）」為已歸還（若尚未結案）
            var prevBorrow = await _db.BorrowRecords
                .Where(b => b.ListingID == r.ListingID
                         && b.StatusCode == (byte)BorrowCondition.Borrowed
                         && b.ReturnDate == null)
                .OrderByDescending(b => b.BorrowDate)
                .FirstOrDefaultAsync(ct);

            if (prevBorrow != null)
            {
                // 視你的規則決定歸還日：now.Date 或先前已知的還書日
                prevBorrow.ReturnDate = now.Date;
                prevBorrow.StatusCode = (byte)BorrowCondition.Returned;
                prevBorrow.UpdatedAt = now;
                // 若要計算逾期/罰金，也可在此處理（略）
            }

            int days = borrowDays ?? DefaultBorrowDays;

            _db.BorrowRecords.Add(new BorrowRecord
            {
                ListingID = r.ListingID,
                MemberID = r.MemberID,     // 僅限預約者本人
                BorrowDate = now,
                DueDate = now.Date.AddDays(days),
                StatusCode = (byte)BorrowCondition.Borrowed,
                CreatedAt = now,
                UpdatedAt = now,
                ReservationID = null
            });

            r.Status = (byte)ReservationStatus.Complete;  // 完成借閱
            r.ReadyAt = now;
            r.ExpiresAt = null;

            r.Listing.IsAvailable = false; // 已借出 → 不可借

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, null);
        }
        // 取有效的 Wait（保留者）
        public async Task<Reservation?> GetActiveHoldAsync(int listingId, CancellationToken ct = default)
        {
            var now = DateTime.Now;
            //var now = DateTime.UtcNow;測試自動逾期用
            return await _db.Reservations
                .Include(r => r.Member)
                .Where(r => r.ListingID == listingId
                         && r.Status == (byte)ReservationStatus.Wait
                         && (r.ExpiresAt == null || r.ExpiresAt >= now))
                .OrderBy(r => r.ReservationAt)
                .FirstOrDefaultAsync(ct);
        }
    }
}
