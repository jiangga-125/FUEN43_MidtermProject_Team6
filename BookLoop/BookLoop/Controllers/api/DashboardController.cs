using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers.api
{
    [Route("api/dashboard")]
    [ApiController]
    [AllowAnonymous]
    public class DashboardController : ControllerBase
    {
        private readonly ShopDbContext _shop;           // 訂單/商店資料（Orders）
        private readonly BookSystemContext _bookSys;    // 書籍/庫存資料（BookInventory）
        private readonly BorrowContext _borrow;         // 借閱資料（BorrowRecords）

        public DashboardController(
            ShopDbContext shop,
            BookSystemContext bookSys,
            BorrowContext borrow)
        {
            _shop = shop;
            _bookSys = bookSys;
            _borrow = borrow;
        }

        // ========= 上方四個卡片 + 最新訂單 =========
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 今日訂單數（所有狀態）
            var todayOrderCount = await _shop.Set<Order>()
                .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow)
                .CountAsync();

            // 今日營收（排除已取消 Status == 4）
            var todayRevenue = await _shop.Set<Order>()
                .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow && o.Status != 4)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            // 未出貨（定義為 Status == 0 或 1）
            var unshippedCount = await _shop.Set<Order>()
                .Where(o => o.Status == 0 || o.Status == 1)
                .CountAsync();

            // 最新訂單（最多 5 筆）
            var latestOrdersRaw = await _shop.Set<Order>()
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new
                {
                    o.OrderID,
                    o.MemberID,
                    o.TotalAmount,
                    o.Status,
                    o.CreatedAt
                })
                .ToListAsync();

            var latestOrders = latestOrdersRaw
                .Select(o => new
                {
                    Id = o.OrderID,
                    MemberId = o.MemberID,
                    o.TotalAmount,
                    o.Status,
                    StatusText =
                        o.Status == 0 ? "待付款" :
                        o.Status == 1 ? "已下訂" :
                        o.Status == 2 ? "已出貨" :
                        o.Status == 3 ? "完成訂單" :
                        o.Status == 4 ? "已取消" : "未知狀態",
                    o.CreatedAt
                })
                .ToList();

            // 庫存告警
            int inventoryAlerts;
            try
            {
                inventoryAlerts = await _bookSys.Set<BookInventory>()
                    .Where(b => b.OnHand - b.Reserved <= 5)
                    .CountAsync();
            }
            catch
            {
                inventoryAlerts = 0;
            }

            return Ok(new
            {
                todayOrderCount,
                todayRevenue,
                unshippedCount,
                inventoryAlerts,
                latestOrders
            });
        }

        // ========= 近 14 天銷售趨勢（訂單數 / 營收） =========
        [HttpGet("sales14")]
        public async Task<IActionResult> Sales14([FromQuery] string type = "revenue")
        {
            type = (type ?? "revenue").ToLowerInvariant();

            var end = DateTime.Today;
            var start = end.AddDays(-13);

            var baseQuery = _shop.Set<Order>()
                .Where(o => o.OrderDate >= start &&
                            o.OrderDate < end.AddDays(1) &&
                            o.Status != 4); // 排除已取消

            var labels = Enumerable.Range(0, 14)
                .Select(i => start.AddDays(i).ToString("MM/dd"))
                .ToArray();

            double[] data;

            if (type == "orders")
            {
                var rows = await baseQuery
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();

                data = Enumerable.Range(0, 14)
                    .Select(i =>
                    {
                        var d = start.AddDays(i).Date;
                        var row = rows.FirstOrDefault(r => r.Date == d);
                        return (double)(row?.Count ?? 0);
                    })
                    .ToArray();
            }
            else
            {
                var rows = await baseQuery
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Sum = g.Sum(x => (decimal?)x.TotalAmount) ?? 0m
                    })
                    .ToListAsync();

                data = Enumerable.Range(0, 14)
                    .Select(i =>
                    {
                        var d = start.AddDays(i).Date;
                        var row = rows.FirstOrDefault(r => r.Date == d);
                        return (double)(row?.Sum ?? 0m);
                    })
                    .ToArray();

                type = "revenue";
            }

            return Ok(new { labels, data, type });
        }

        // ========= 近 14 天借閱本數（總借閱 / 借閱中） =========
        [HttpGet("borrow14")]
        public async Task<IActionResult> Borrow14([FromQuery] string type = "total")
        {
            type = (type ?? "total").ToLowerInvariant();

            var end = DateTime.Today;
            var start = end.AddDays(-13);

            var baseQuery = _borrow.BorrowRecords
                .AsNoTracking()
                .Where(b => b.BorrowDate >= start &&
                            b.BorrowDate < end.AddDays(1));

            // 借閱中：狀態 Borrowed 或 Overdue，且尚未歸還
            if (type == "borrowing")
            {
                baseQuery = baseQuery.Where(b =>
                    (b.StatusCode == (byte)BorrowRecord.BorrowStatus.Borrowed ||
                     b.StatusCode == (byte)BorrowRecord.BorrowStatus.Overdue) &&
                    b.ReturnDate == null);
            }
            else
            {
                type = "total"; // 其他值一律當作 total
            }

            var rows = await baseQuery
                .GroupBy(b => b.BorrowDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var labels = new string[14];
            var data = new int[14];

            for (int i = 0; i < 14; i++)
            {
                var d = start.AddDays(i).Date;
                labels[i] = d.ToString("MM/dd");
                var row = rows.FirstOrDefault(r => r.Date == d);
                data[i] = row?.Count ?? 0;
            }

            return Ok(new { labels, data, type });
        }

        // ========= 借閱紀錄 =========
        [HttpGet("latest-borrows")]
        public async Task<IActionResult> LatestBorrows()
        {
            // 取最近 5 筆借閱（按 BorrowDate / RecordID 倒序）
            var raw = await _borrow.BorrowRecords
                .AsNoTracking()
                .Include(b => b.Listing)
                .OrderByDescending(b => b.BorrowDate)
                .ThenByDescending(b => b.RecordID)
                .Take(5)
                .Select(b => new
                {
                    b.RecordID,
                    b.MemberID,
                    ListingTitle = b.Listing.Title,
                    b.BorrowDate,
                    b.DueDate,
                    b.StatusCode
                })
                .ToListAsync();

            var items = raw.Select(b => new
            {
                Id = b.RecordID,
                MemberId = b.MemberID,
                ListingTitle = b.ListingTitle,
                BorrowDate = b.BorrowDate,
                DueDate = b.DueDate,
                StatusText =
                    b.StatusCode == (byte)BorrowRecord.BorrowStatus.Borrowed ? "借出" :
                    b.StatusCode == (byte)BorrowRecord.BorrowStatus.Overdue ? "逾期" :
                    b.StatusCode == (byte)BorrowRecord.BorrowStatus.Returned ? "歸還" : "-"
            }).ToList();

            // 前端預期格式：{ latestBorrows: [...] }
            return Ok(new { latestBorrows = items });
        }
    }
}
