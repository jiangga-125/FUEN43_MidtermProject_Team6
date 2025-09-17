using Microsoft.EntityFrameworkCore;
using System;
using BorrowSystem.ViewModels;
using BookLoop.Models;
namespace BorrowSystem.Services
{
    public class ReservationExpiryService
    {
        private readonly BorrowSystemContext _context;
        public ReservationExpiryService(BorrowSystemContext context)
        {
            _context = context;
        }
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
                                      
            // 3) 將對應的書籍設為可借（在一本書僅一預約規則下）
            await _context.Listings
                .Where(l => listingIds.Contains(l.ListingID))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.IsAvailable, true)
                    ,ct);                  

            await tx.CommitAsync(ct);
            return overdue.Count;
        }

    }
}
