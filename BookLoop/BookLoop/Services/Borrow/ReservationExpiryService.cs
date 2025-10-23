using Microsoft.EntityFrameworkCore;
using System;
using BookLoop.Models;
using BookLoop.ViewModels;
namespace BookLoop.Services
{
    public class ReservationExpiryService
    {
        private readonly BorrowContext _context;
        public ReservationExpiryService(BorrowContext context)
        {
            _context = context;
        }
        // 定期檢查並過期逾期的預約:借閱中的預約
        public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
        {
            var now = DateTime.Now;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            // 1) 找出所有已逾期的等待取書預約
            var overdue = await _context.Reservations
                .Where(r => r.Status == (byte)ReservationStatus.Wait
                         && r.ExpiresAt != null
                         && r.ExpiresAt <= now)
                .Select(r => new { r.ReservationID, r.ListingID })
                .ToListAsync(ct);

            if (overdue.Count == 0)
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            var reservationIds = overdue.Select(x => x.ReservationID).ToList();
            var listingIds = overdue.Select(x => x.ListingID).Distinct().ToList();

            // 2) 將預約標記為 AutoExpired
            await _context.Reservations
                .Where(r => reservationIds.Contains(r.ReservationID))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, (byte)ReservationStatus.AutoExpired), ct);
                                      
                             

            await tx.CommitAsync(ct);
            return overdue.Count;
        }

    }
}
