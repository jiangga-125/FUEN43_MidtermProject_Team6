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
            var bookLoopContext = _context.PenaltyTransactions.Include(p => p.Member).Include(p => p.Record).Include(p => p.Rule);
            return View(await bookLoopContext.ToListAsync());
        }






        // GET: PenaltyTransactions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var penaltyTransaction = await _context.PenaltyTransactions
                .Include(p => p.Member)
                .Include(p => p.Record)
                .Include(p => p.Rule)
                .FirstOrDefaultAsync(m => m.PenaltyID == id);
            if (penaltyTransaction == null)
            {
                return NotFound();
            }

            return View(penaltyTransaction);
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
            .Select(r => new { r.RuleID, r.ReasonCode, r.UnitAmount,r.ChargeType })
            .ToList();
            // 排除的規則:逾期
            var excludedRuleIds = new[] { 4 }; 

            var query = _context.PenaltyRules
                .AsNoTracking()
                .Where(r => r.IsActive);          // 第一層：只取啟用的

            if (excludedRuleIds?.Length > 0)
            {
                query = query.Where(r => !excludedRuleIds.Contains(r.RuleID)); // 第二層：排除特定ID
            }

            var selectRules = await query
                .OrderBy(r => r.ReasonCode)
                .ToListAsync();

            ViewData["RuleID"] = new SelectList(selectRules, "RuleID", "ReasonCode");
            ViewBag.ChargeType = rules.ToDictionary(r => r.RuleID, r => r.ChargeType);
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
            var rule = await _context.PenaltyRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RuleID == vm.RuleID && r.IsActive);

            if (rule == null)
            {
                ModelState.AddModelError(nameof(vm.RuleID), "無效的罰款原因或已停用。");
            }

            

            if (!ModelState.IsValid)
            {
              
                var excludedRuleIds = new[] { 4 }; // 與 GET 一致
                var query = _context.PenaltyRules.AsNoTracking().Where(r => r.IsActive);
                if (excludedRuleIds?.Length > 0)
                {
                    query = query.Where(r => !excludedRuleIds.Contains(r.RuleID));
                }
                var selectRules = await query.OrderBy(r => r.ReasonCode).ToListAsync();
                ViewData["RuleID"] = new SelectList(selectRules, "RuleID", "ReasonCode", vm.RuleID);

                var rules = await _context.PenaltyRules
                    .Select(r => new { r.RuleID, r.ReasonCode, r.UnitAmount, r.ChargeType })
                    .ToListAsync();
                ViewBag.ChargeType = rules.ToDictionary(r => r.RuleID, r => r.ChargeType);

                // 保留從 GET 帶來的顯示資訊
                vm.MemberName = borrowRecord.Member?.Username;

                return View(vm);
            }

            // 建立交易實體（請把欄位改成你專案的實際欄位）
            var entity = new PenaltyTransaction
            {
                // TransactionID 由資料庫產生則不用設定
                MemberID = vm.MemberID,
                RecordID = vm.RecordID,
                RuleID = vm.RuleID,
                Quantity = vm.Quantity,
                //UnitAmount = rule.UnitAmount,
                PaidAt = null, // 初始為未付款

                CreatedAt = DateTime.UtcNow  // 或 DateTime.Now 依專案時區策略
            };

            _context.Add(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "已建立扣款紀錄。";


            return RedirectToAction("Index", "PenaltyTransactions");
        }

       
    }
}
