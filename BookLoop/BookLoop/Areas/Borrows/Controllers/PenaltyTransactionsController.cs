using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;

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
        public IActionResult Create()
        {
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username");
            ViewData["RecordID"] = new SelectList(_context.BorrowRecords, "RecordID", "RecordID");
            ViewData["RuleID"] = new SelectList(_context.PenaltyRules, "RuleID", "ChargeType");
            return View();
        }

        // POST: PenaltyTransactions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PenaltyID,RecordID,MemberID,RuleID,CreatedAt,Quantity,PaidAt")] PenaltyTransaction penaltyTransaction)
        {
            if (ModelState.IsValid)
            {
                _context.Add(penaltyTransaction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", penaltyTransaction.MemberID);
            ViewData["RecordID"] = new SelectList(_context.BorrowRecords, "RecordID", "RecordID", penaltyTransaction.RecordID);
            ViewData["RuleID"] = new SelectList(_context.PenaltyRules, "RuleID", "ChargeType", penaltyTransaction.RuleID);
            return View(penaltyTransaction);
        }

        // GET: PenaltyTransactions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var penaltyTransaction = await _context.PenaltyTransactions.FindAsync(id);
            if (penaltyTransaction == null)
            {
                return NotFound();
            }
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", penaltyTransaction.MemberID);
            ViewData["RecordID"] = new SelectList(_context.BorrowRecords, "RecordID", "RecordID", penaltyTransaction.RecordID);
            ViewData["RuleID"] = new SelectList(_context.PenaltyRules, "RuleID", "ChargeType", penaltyTransaction.RuleID);
            return View(penaltyTransaction);
        }

        // POST: PenaltyTransactions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PenaltyID,RecordID,MemberID,RuleID,CreatedAt,Quantity,PaidAt")] PenaltyTransaction penaltyTransaction)
        {
            if (id != penaltyTransaction.PenaltyID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(penaltyTransaction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PenaltyTransactionExists(penaltyTransaction.PenaltyID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["MemberID"] = new SelectList(_context.Members, "MemberID", "Username", penaltyTransaction.MemberID);
            ViewData["RecordID"] = new SelectList(_context.BorrowRecords, "RecordID", "RecordID", penaltyTransaction.RecordID);
            ViewData["RuleID"] = new SelectList(_context.PenaltyRules, "RuleID", "ChargeType", penaltyTransaction.RuleID);
            return View(penaltyTransaction);
        }

        // GET: PenaltyTransactions/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

        // POST: PenaltyTransactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var penaltyTransaction = await _context.PenaltyTransactions.FindAsync(id);
            if (penaltyTransaction != null)
            {
                _context.PenaltyTransactions.Remove(penaltyTransaction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PenaltyTransactionExists(int id)
        {
            return _context.PenaltyTransactions.Any(e => e.PenaltyID == id);
        }
    }
}
