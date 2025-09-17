using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using BorrowSystem.ViewModels;
using BookLoop.Models;


namespace BorrowSystem.Controllers
{
    [Area("Borrow")]
    public class ListingsController : Controller
    {
        private readonly BorrowSystemContext _context;

        public ListingsController(BorrowSystemContext context)
        {
            _context = context;
        }

        // GET: Listings
        public async Task<IActionResult> Index(string? q, int? CategoryID, int? PublisherID, int page = 1, int pageSize = 10)
        {
          
            var query = _context.Listings

                .AsNoTracking()     // 只讀列表
                .AsSplitQuery()     // 多 Include 時避免巨大 SQL
                .Include(l => l.Category)
                .Include(l => l.Publisher)
                .Include(l => l.ListingAuthors)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(l => EF.Functions.Like(l.Title, pattern) || EF.Functions.Like(l.ISBN, pattern));
            }

            if (CategoryID.HasValue)
                query = query.Where(l => l.CategoryID == CategoryID);

            if (PublisherID.HasValue)
                query = query.Where(l => l.PublisherID == PublisherID);

            var total = await query.CountAsync();
            var list = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["CategoryID"] = new SelectList(await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync(), "CategoryID", "CategoryName", CategoryID);
            ViewData["PublisherID"] = new SelectList(await _context.Publishers.OrderBy(p => p.PublisherName).ToListAsync(), "PublisherID", "PublisherName", PublisherID);

            ViewData["Total"] = total;
            ViewData["Page"] = page;
            ViewData["PageSize"] = pageSize;

            // AJAX：只回傳部分
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_IndexTable", list);
            return View(list);
        }

        // GET: Listings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
    if (id == null) return NotFound();

    var vm = await _context.Listings
        .AsNoTracking()
        .Where(l => l.ListingID == id)
        .Select(l => new ListingsCrudForDetailDelete
        {
            ListingID = l.ListingID,
            Title = l.Title,
            ISBN = l.ISBN,
            Condition = l.Condition,
            CreatedAt = l.CreatedAt,
            IsAvailable = l.IsAvailable,
            CategoryName = l.Category != null ? l.Category.CategoryName : "",
            PublisherName = l.Publisher != null ? l.Publisher.PublisherName : "",
            AuthorNames = l.ListingAuthors
                .OrderByDescending(la => la.IsPrimary)
                .ThenBy(la => la.ListingAuthorID)
                .Select(la => la.AuthorName + (la.IsPrimary ? "（主作者）" : "（次作者）"))
                .ToList()
        })
        .FirstOrDefaultAsync();

    if (vm == null) return NotFound();

    return View(vm);
}
        private async Task FillSelectsAsync(ListingFormViewModel vm)
        {
            vm.Categories = (await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .ToListAsync())
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryID.ToString(),
                    Text = c.CategoryName,
                    Selected = vm.CategoryID.HasValue && vm.CategoryID.Value == c.CategoryID
                });

            vm.Publishers = (await _context.Publishers
                .AsNoTracking()
                .OrderBy(p => p.PublisherName)
                .ToListAsync())
                .Select(p => new SelectListItem
                {
                    Value = p.PublisherID.ToString(),
                    Text = p.PublisherName,
                    Selected = vm.PublisherID.HasValue && vm.PublisherID.Value == p.PublisherID
                });
        }

        // GET: Listings/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new ListingsCrudForCreateEdit
            {
                Authors = new List<AuthorItem>
        {
            new AuthorItem { AuthorName = ""} // 預設第一位是主作者
        },
                PrimaryIndex = 0
            };
            await PopulateDropdownsAsync(vm);
            return View(vm);
        }
    

        // POST: Listings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ListingsCrudForCreateEdit vm)
        {
            vm.Authors = (vm.Authors ?? new List<AuthorItem>())
            .Select(a => new AuthorItem { ListingAuthorID = a.ListingAuthorID, AuthorName = a.AuthorName?.Trim() ?? "" })
            .Where(a => !string.IsNullOrWhiteSpace(a.AuthorName))
            .ToList();

            if (vm.Authors.Count == 0)
                ModelState.AddModelError(nameof(vm.Authors), "至少需要 1 位作者");

            if (vm.PrimaryIndex == null || vm.PrimaryIndex < 0 || vm.PrimaryIndex >= vm.Authors.Count)
                ModelState.AddModelError(nameof(vm.PrimaryIndex), "請選擇主作者");


            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(vm);
                return View(vm); // 驗證失敗直接回畫面，不要 Redirect
            }
            var primaryIndex = vm.PrimaryIndex!.Value;
            // 3) 實際存檔
            try
            {
                var listing = new Listing
                {
                    Title = vm.Title.Trim(),
                    ISBN = vm.ISBN.Trim(),
                    Condition = vm.Condition,
                    IsAvailable = vm.IsAvailable,
                    CategoryID = vm.CategoryID!.Value,
                    PublisherID = vm.PublisherID!.Value,
                    CreatedAt = DateTime.UtcNow,
                    // 確保集合已初始化（若你的實體類別沒在屬性上初始化）
                    ListingAuthors = new List<ListingAuthor>()
                };

                for (int i = 0; i < vm.Authors.Count; i++)
                {
                    listing.ListingAuthors.Add(new ListingAuthor
                    {
                        AuthorName = vm.Authors[i].AuthorName!,
                        IsPrimary = (i == primaryIndex),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.Listings.Add(listing);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "新增成功。";
                return RedirectToAction(nameof(Index)); 
            }
            catch (DbUpdateException ex)
            {
                // 4) 發生例外時，把錯誤顯示出來，避免「以為成功但其實沒存」
                ModelState.AddModelError(string.Empty, $"存檔失敗：{ex.GetBaseException().Message}");
                await PopulateDropdownsAsync(vm);
                return View(vm);
            }
        }

        private async Task PopulateDropdownsAsync(ListingsCrudForCreateEdit vm)
        {
            vm.CategoryOptions = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryID.ToString(),
                    Text = c.CategoryName,
                    Selected = (c.CategoryID == vm.CategoryID)
                })
                .ToListAsync();

            vm.PublisherOptions = await _context.Publishers
                .OrderBy(p => p.PublisherName)
                .Select(p => new SelectListItem
                {
                    Value = p.PublisherID.ToString(),
                    Text = p.PublisherName,
                    Selected = (p.PublisherID == vm.PublisherID)
                })
                .ToListAsync();
        }




        // GET: Listings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var listing = await _context.Listings
            .Include(l => l.ListingAuthors)
            .FirstOrDefaultAsync(l => l.ListingID == id);

            if (listing == null) return NotFound();
            var authorsVm = listing.ListingAuthors
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.ListingAuthorID)
            .Select(a => new AuthorItem
            {
           ListingAuthorID = a.ListingAuthorID,
           AuthorName = a.AuthorName,
           IsPrimary = a.IsPrimary
            })
            .ToList();

            var vm = new ListingsCrudForCreateEdit
            {
                ListingID = listing.ListingID,
                Title = listing.Title,
                ISBN = listing.ISBN,
                Condition = listing.Condition,
                IsAvailable = listing.IsAvailable,
                CategoryID = listing.CategoryID,
                PublisherID = listing.PublisherID,
                Authors = authorsVm,
                PrimaryIndex = Math.Max(0, authorsVm.FindIndex(a => a.IsPrimary))
            };
            await PopulateDropdownsAsync(vm);
            return View(vm);
        }

        // POST: Listings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ListingsCrudForCreateEdit vm)
        {
            if (id != vm.ListingID) return NotFound();

            vm.Authors = (vm.Authors ?? new List<AuthorItem>())
            .Select(a => new AuthorItem { ListingAuthorID = a.ListingAuthorID, AuthorName = a.AuthorName?.Trim() ?? "" })
            .Where(a => !string.IsNullOrWhiteSpace(a.AuthorName))
            .ToList();
            if (vm.PrimaryIndex == null || vm.PrimaryIndex < 0 || vm.PrimaryIndex >= vm.Authors.Count)
                ModelState.AddModelError(nameof(vm.PrimaryIndex), "請選擇主作者");
                      

            if (!ModelState.IsValid) {
                await PopulateDropdownsAsync(vm);
                return View(vm); 
            }
            var primaryIndex = vm.PrimaryIndex!.Value;
            var listing = await _context.Listings
                .Include(l => l.ListingAuthors)
                .FirstOrDefaultAsync(l => l.ListingID == id);

            if (listing == null) return NotFound();

            // 更新 Listing 本身欄位
            listing.Title = vm.Title;
            listing.ISBN = vm.ISBN;
            listing.Condition = vm.Condition;
            listing.IsAvailable = vm.IsAvailable;
            listing.CategoryID = vm.CategoryID!.Value;
            listing.PublisherID = vm.PublisherID!.Value;

            // 先取出既有作者
            var existing = listing.ListingAuthors.ToList();
            ListingAuthor? newPrimaryRow = null; // 若選的是新加的列，用這個承接

            for (int i = 0; i < vm.Authors.Count; i++)
            {
                var a = vm.Authors[i];

                if (a.ListingAuthorID.HasValue)
                {
                    var row = existing.FirstOrDefault(x => x.ListingAuthorID == a.ListingAuthorID.Value);
                    if (row != null)
                    {
                        row.AuthorName = a.AuthorName!;
                        row.IsPrimary = false; // 先全部清 0，避免唯一索引衝突
                    }
                }
                else
                {
                    var added = new ListingAuthor
                    {
                        AuthorName = a.AuthorName!,
                        IsPrimary = false,       // 先清 0
                        ListingID = listing.ListingID,
                        CreatedAt = DateTime.UtcNow
                    };
                    listing.ListingAuthors.Add(added);

                    // 如果這一列剛好是選為主作者的列，先記起來，等拿到 ID 後再設 1
                    if (i == vm.PrimaryIndex!.Value) newPrimaryRow = added;
                }
            }


            // 移除已刪掉的作者
            var keepIds = vm.Authors.Where(a => a.ListingAuthorID.HasValue)
                                    .Select(a => a.ListingAuthorID.Value)
                                    .ToHashSet();
            foreach (var r in existing.Where(x => !keepIds.Contains(x.ListingAuthorID)).ToList())
                _context.ListingAuthors.Remove(r);
            await _context.SaveChangesAsync();
            int primaryRowId;
            var chosen = vm.Authors[vm.PrimaryIndex!.Value];

            if (chosen.ListingAuthorID.HasValue)
            {
                primaryRowId = chosen.ListingAuthorID.Value;
            }
            else
            {
                // 新增的那筆：SaveChanges 後 newPrimaryRow 會有 ListingAuthorID
                if (newPrimaryRow == null)
                {
                    // 理論上不會發生，保險防呆
                    await PopulateDropdownsAsync(vm);
                    ModelState.AddModelError("", "主作者對應不到任何列，請再試一次。");
                    return View(vm);
                }
                primaryRowId = newPrimaryRow.ListingAuthorID;
            }
            var primaryEntity = await _context.ListingAuthors.FirstAsync(x => x.ListingAuthorID == primaryRowId);
            primaryEntity.IsPrimary = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "修改成功。";
            return RedirectToAction(nameof(Index));
        }

        // GET: Listings/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var info = await _context.Listings
            .Where(l => l.ListingID == id)
            .Select(l => new { l.ListingID, l.Title, l.IsAvailable })
            .FirstOrDefaultAsync();

            if (info == null) return NotFound();

            // 禁止進入 Create
            if (!info.IsAvailable)
            {
                TempData["ErrorMessage"] = "此書已在借閱中，不能刪除。";
                return RedirectToAction("Index", "Listings", new { id });
            }


            var vm = await _context.Listings
                .Where(l => l.ListingID == id)
                .Select(l => new BorrowSystem.ViewModels.ListingsCrudForDetailDelete
                {
                    ListingID = l.ListingID,
                    Title = l.Title,
                    ISBN = l.ISBN,
                    Condition = l.Condition,
                    CreatedAt = l.CreatedAt,
                    IsAvailable = l.IsAvailable,
                    CategoryName = l.Category.CategoryName,
                    PublisherName = l.Publisher.PublisherName,
                    AuthorNames = l.ListingAuthors.Select(ba => ba.AuthorName).ToList()
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return View(vm);
        }

        // POST: Listings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var exists = await _context.Listings
                                 .AsNoTracking()
                                 .AnyAsync(l => l.ListingID == id);
                if (!exists)
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = "資料已不存在（可能已被刪除）。";
                    return RedirectToAction(nameof(Index));
                }

                // 若沒有設定 FK cascade，就先刪中介/子表
                await _context.ListingAuthors
                              .Where(la => la.ListingID == id)
                              .ExecuteDeleteAsync(); // EF Core 7+

                // 刪主表（可用 stub 避免再次查詢）
                _context.Listings.Remove(new Listing { ListingID = id });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "刪除成功。";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException dbEx)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "刪除失敗（可能有關聯資料）： " + dbEx.Message);

                var vm = await BuildDeleteVmAsync(id);
                if (vm == null) return RedirectToAction(nameof(Index));
                return View("Delete", vm); // ✅ 回傳 View 期望的 ViewModel
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "刪除失敗：" + ex.Message);

                var vm = await BuildDeleteVmAsync(id);
                if (vm == null) return RedirectToAction(nameof(Index));
                return View("Delete", vm); // ✅ 回傳 View 期望的 ViewModel
            }
        }
        private Task<BorrowSystem.ViewModels.ListingsCrudForDetailDelete?> BuildDeleteVmAsync(int id)
        {
            return _context.Listings
                .Where(l => l.ListingID == id)
                .Select(l => new BorrowSystem.ViewModels.ListingsCrudForDetailDelete
                {
                    ListingID = l.ListingID,
                    Title = l.Title,                 
                    ISBN = l.ISBN,                  
                    Condition = l.Condition,
                    CreatedAt = l.CreatedAt,
                    IsAvailable = l.IsAvailable,
                    CategoryName = l.Category.CategoryName, 
                    PublisherName = l.Publisher.PublisherName,        
                    AuthorNames = l.ListingAuthors
                                        .Select(ba => ba.AuthorName)
                                        .ToList()
                })
                .FirstOrDefaultAsync();
        }

        private bool ListingExists(int id)
        {
            return _context.Listings.Any(e => e.ListingID == id);
        }
    }
}
