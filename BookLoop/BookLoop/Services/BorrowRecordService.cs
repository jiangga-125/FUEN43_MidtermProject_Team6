using Microsoft.EntityFrameworkCore;
using System;
using static BorrowSystem.ViewModels.BorrowRecordsViewModel;
using BookLoop.Models;

namespace BorrowSystem.Services
{
    public class BorrowRecordService
    {
        private readonly BorrowSystemContext _context;
        public BorrowRecordService(BorrowSystemContext context)
        {
            _context = context;
        }
        //歸還逾期使用
        public async Task UpdateOverdueAsync()
        {
            var today = DateTime.Today;
            var overdueList = await _context.BorrowRecords
                .Where(br => br.StatusCode == (byte)BorrowCondition.Borrowed
                          && today > br.DueDate.Date)
                .ToListAsync();

            foreach (var br in overdueList)
            {
                br.StatusCode = (byte)BorrowCondition.Overdue;
                br.UpdatedAt = DateTime.Now;
            }

            if (overdueList.Count > 0)
                await _context.SaveChangesAsync();
        }
    }
}
