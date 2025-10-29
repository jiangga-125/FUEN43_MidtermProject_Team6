using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoop.Controllers
{
    [Area("Borrows")]
    public class PenaltyRulesController : Controller
    {
        private readonly BorrowContext _context;

        public PenaltyRulesController(BorrowContext context)
        {
            _context = context;
        }
        // 按編輯（塞進 index的 Modal）
        [HttpGet]
        public IActionResult EditRow(int id)
        {
            var editRow = _context.PenaltyRules.Find(id);
            //EF 會用 PenaltyRule 這個實體的「主鍵」 來查資料庫
            if (editRow == null)
            {
                return Content("<div class='text-danger p-2'>查無資料</div>", "text/html");
                //沒有資料則顯示
               
            }
                return PartialView("_RuleEditRow", editRow);
        }

        // GET: PenaltyRules
        public async Task<IActionResult> Index()
        {
            return View(await _context.PenaltyRules.ToListAsync());
        }
        // 整張表（塞進 Modal）
        [HttpGet]
        public IActionResult TablePartial()
        {
            var list = _context.PenaltyRules.OrderBy(r => r.RuleID).ToList();
            return PartialView("_RulePartial", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveRow(PenaltyRule model)
        {
            if (string.IsNullOrWhiteSpace(model.ReasonCode))
                return Json(new { ok = false, message = "罰款原因不可空白" });
            if (string.IsNullOrWhiteSpace(model.ChargeType))
                return Json(new { ok = false, message = "罰款類型不可空白" });
            if (model.ChargeType.Length > 20)
                return Json(new { ok = false, message = "罰款類型過長（最多 20 字）" });
            if (model.UnitAmount < 0)
                return Json(new { ok = false, message = "單價不可小於 0" });

            try
            {
                // 1) 先抓舊資料
                var entity = _context.PenaltyRules.Find(model.RuleID);
                if (entity == null)
                    return Json(new { ok = false, message = "查無資料" });

                // 2) 只更新允許編輯的欄位
                entity.ReasonCode = model.ReasonCode;
                entity.ChargeType = model.ChargeType;
                entity.UnitAmount = model.UnitAmount;

                bool wasActive = entity.IsActive;//資料庫目前的狀態（儲存前的舊值）
                bool willBeActive = model.IsActive;//使用者這次送出想要的狀態（表單的新值）。

                if (!wasActive&&willBeActive)
                {
                    entity.IsActive = true;
                    entity.EffectiveFrom = DateTime.Now;
                    entity.EffectiveTo = null;
                }
                else if (wasActive && !willBeActive)
                {
                    // 啟用 → 停用
                    entity.IsActive = false;
                    entity.EffectiveTo = DateTime.Now;
                }
                else
                {
                    // 狀態未變：不動日期
                    entity.IsActive = willBeActive;
                }
                _context.SaveChanges();
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "儲存失敗：" + ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Deactivate(int id)
        {
            try
            {
                var entity = _context.PenaltyRules.Find(id);
                if (entity == null) return Json(new { ok = false, message = "查無資料" });
                
                entity.IsActive = false;
                entity.EffectiveTo = DateTime.Today;
                _context.SaveChanges();
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "停用失敗：" + ex.Message });
            }
        }
        
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReasonCode,ChargeType,UnitAmount")] PenaltyRule penaltyRule)
        {
            if (string.IsNullOrWhiteSpace(penaltyRule.ReasonCode))
                ModelState.AddModelError(nameof(PenaltyRule.ReasonCode), "ReasonCode 為必填");

            if (string.IsNullOrWhiteSpace(penaltyRule.ChargeType))
                ModelState.AddModelError(nameof(PenaltyRule.ChargeType), "ChargeType 為必填");

            
            if (ModelState.TryGetValue(nameof(PenaltyRule.UnitAmount), out var entry))
                entry.Errors.Clear(); 

            if (!Request.HasFormContentType ||
                !Request.Form.ContainsKey("UnitAmount") ||
                string.IsNullOrWhiteSpace(Request.Form["UnitAmount"]))
            {
                ModelState.AddModelError(nameof(PenaltyRule.UnitAmount), "UnitAmount 為必填");
            }
            else if (penaltyRule.UnitAmount <= 0)
            {
                ModelState.AddModelError(nameof(PenaltyRule.UnitAmount), "UnitAmount 必須大於 0");
            }

            if (!ModelState.IsValid) return View(penaltyRule);

            // server端決定欄位
            penaltyRule.EffectiveFrom = DateTime.Now; 
            penaltyRule.EffectiveTo = null;
            penaltyRule.IsActive = true;

            _context.Add(penaltyRule);
            await _context.SaveChangesAsync();
           
            TempData["FlashMessage"] = "新增成功！";
            return RedirectToAction(nameof(Index));
            

        }

    }
}
