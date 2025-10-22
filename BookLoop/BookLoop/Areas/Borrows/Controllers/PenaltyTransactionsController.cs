using BookLoop.Models;
using BookLoop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers
{
    [Area("Borrows")]
    public class PenaltyTransactionsController : Controller
    {
        private readonly BorrowContext _context;

        public PenaltyTransactionsController(BorrowContext context)
        {
            _context = context;
        }

        // GET: PenaltyTransactions
        public async Task<IActionResult> Index()
        {
            var items = await _context.PenaltyTransactions
            .AsNoTracking()
            .Include(x => x.Member)
            .Include(x => x.Rule) // 假設 Rule 內含 ReasonCode/ChargeType/UnitAmount
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PenaltyTransactionsViewModel
            {
                PenaltyID = x.PenaltyID,
                MemberName = x.Member.Username,
                ReasonCode = x.Rule.ReasonCode,
                ChargeType = x.Rule.ChargeType,
                UnitAmount = x.Rule.UnitAmount,
                Quantity = x.Quantity,
                PaidAt = x.PaidAt,
                totalMoney=x.Quantity*x.Rule.UnitAmount//計算總價

            })
            .ToListAsync();

            return View(items);
        }
      
        // GET: PenaltyTransactions/Create
        public async Task<IActionResult> Create(int memberID,int recordID)
        {
            var vm = await _context.BorrowRecords
            .AsNoTracking()
            .Where(m => m.MemberID == memberID && m.RecordID==recordID)
            .Select(m => new PenaltyTransactionsViewModel   // 同時取回需要的欄位
            {
                MemberID = m.MemberID,
                MemberName = m.Member.Username,
                RecordID =m.RecordID,
                UnitAmount = 50 // 預設值，可根據需求調整
            }).FirstOrDefaultAsync();
                            
            if (vm == null)
            {
                TempData["ErrorMessage"] = "沒有這個會員。";
                return RedirectToAction("Index", "BorrowRecords");
            }
            var rules = _context.PenaltyRules
            .Select(r => new { r.RuleID, r.ReasonCode, r.UnitAmount })
            .ToList();
            

            var query = _context.PenaltyRules
                .AsNoTracking()
                .Where(r => r.IsActive);          // 第一層：只取啟用的
           

            var selectRules = await query
                .OrderBy(r => r.ReasonCode)
                .ToListAsync();

            //ViewData["RuleID"] = new SelectList(selectRules, "RuleID", "ReasonCode");
            // 規則下拉
            ViewBag.RuleID = new SelectList(
                await query.OrderBy(r => r.RuleID)                          
                          .ToListAsync(),
                "RuleID", "ReasonCode" /* 或顯示文字欄位 */
            );
            
            ViewBag.UnitAmount = await _context.PenaltyRules
                .Select(r => new { r.RuleID, r.UnitAmount })
                .ToDictionaryAsync(x => x.RuleID, x => x.UnitAmount);
            return View(vm);
        }
        // POST: PenaltyTransactions/Create        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PenaltyTransactionsViewModel vm)
        {
            if (vm == null)
            {
                ModelState.AddModelError(string.Empty, "資料有誤。");
            }
            if (vm.MemberID <= 0 || vm.RecordID <= 0)
            {
                ModelState.AddModelError(string.Empty, "會員或借閱資料不完整。");
            }
            if (vm.RuleID <= 0)
            {
                ModelState.AddModelError(nameof(vm.RuleID), "請選擇扣款原因。");
            }
            // 驗證借閱紀錄是否存在
            var borrowRecord = await _context.BorrowRecords
            .AsNoTracking()
            .Include(b => b.Member)
            .FirstOrDefaultAsync(b => b.MemberID == vm.MemberID && b.RecordID == vm.RecordID);

            if (borrowRecord == null)
            {
                TempData["ErrorMessage"] = "找不到對應的會員或借閱紀錄。";
                return RedirectToAction("Index", "BorrowRecords");
            }

            // 抓選擇的罰則規則
            
            var ruleQuery = _context.PenaltyRules.AsNoTracking().Where(r => r.IsActive);
            
            var rule = await ruleQuery.FirstOrDefaultAsync(r => r.RuleID == vm.RuleID);
            if (rule == null)
            {
                ModelState.AddModelError(nameof(vm.RuleID), "無效的扣款原因或已停用。");
            }


            if (!ModelState.IsValid)
            {
                // 下拉選單資料
                var selectRules = await ruleQuery.OrderBy(r => r.ReasonCode).ToListAsync();
                ViewBag.RuleID = new SelectList(selectRules, "RuleID", "ReasonCode", vm.RuleID);

                // 給前端 JS 用的 { ruleId: unitAmount } 
                ViewBag.UnitAmount = await _context.PenaltyRules
                    .AsNoTracking()
                    .Select(r => new { r.RuleID, r.UnitAmount })
                    .ToDictionaryAsync(x => x.RuleID, x => x.UnitAmount);

                // 顯示用
                vm.MemberName = borrowRecord.Member?.Username ?? vm.MemberName;

                return View(vm);
            }

            var unitAmount = rule.UnitAmount;           // decimal/int 皆可
            var quantity = vm.Quantity;
            var entity = new PenaltyTransaction
            {
                MemberID = vm.MemberID,
                RecordID = vm.RecordID,
                RuleID = vm.RuleID,
                Quantity = quantity,
                // 若資料表有 UnitAmount / TotalAmount 欄位，建議一併存起來（保留當下計價依據）
                // UnitAmount = unitAmount,
                // TotalAmount = totalAmount,
                PaidAt = vm.PaidAt,          // 若你的 UI 有輸入繳清時間，就沿用；若一律未付款可改為 null
                CreatedAt = DateTime.UtcNow  // 時區需求若要台北時間可改用 DateTimeOffset.Now / TimeZoneInfo 轉換
            };

            _context.Add(entity);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "已建立罰款紀錄。";


            return RedirectToAction("Index", "PenaltyTransactions");
        }

        // GET: PenaltyTransactions/Pay/5:繳款用
        public async Task<IActionResult> Pay(int id)
        {
         var record = await _context.PenaltyTransactions
            .FirstOrDefaultAsync(p => p.PenaltyID == id);
            if (record == null)
            {
                TempData["ErrorMessage"] = "找不到罰款紀錄。";
                return RedirectToAction("Index", "PenaltyTransactions");
            }
            record.PaidAt = DateTime.Now; // 設定為目前時間            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "罰款已標記為繳清。";
            return RedirectToAction("Index", "PenaltyTransactions");
        }

    }
}
