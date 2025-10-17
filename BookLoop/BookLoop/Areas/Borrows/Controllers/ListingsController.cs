using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookLoop.Models;
using BookLoop.ViewModels;

namespace BookLoop.Controllers
{
    [Area("Borrows")]
    public class ListingsController : Controller
    {
        private readonly BorrowContext _context;
        private readonly IWebHostEnvironment _env;

        public ListingsController(BorrowContext context, IWebHostEnvironment env )
        {
            _context = context;
            _env = env;
        }

        // GET: Listings
        public  IActionResult Index()
        {
            return View();
        }
        public async Task <IActionResult> IndexFront()
        {
            var bookLoopContext = _context.Listings.Include(l => l.Category).Include(l => l.Publisher);
            return View(await bookLoopContext.ToListAsync());
            
        }
        //
        [HttpGet]
        public async Task<IActionResult> GetBooks()
        {
            var books = await _context.Listings       
                .AsNoTracking()
                .Select(l => new
            {
                    l.ListingID,
                    l.Title,
                    ImageUrl = l.ListingImages.Select(i => i.ImageUrl).FirstOrDefault(),
                    Authors = string.Join(", ", l.ListingAuthors.Select(a => a.AuthorName)),
                    Category = l.Category.CategoryName,
                    Publisher = l.Publisher.PublisherName,
                    l.ISBN,
                    l.Status
                }).ToListAsync();
            return Json(new {data=books});
        }

        // GET: Listings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var vm = await _context.Listings
                .Where(l => l.ListingID == id)
                .Select(l => new ListingsdetailViewModel
                {
                    ListingId = l.ListingID,
                    Title = l.Title,
                    ISBN = l.ISBN,
                    Status = l.Status,
                    Condition = l.Condition,

                    // 分類 / 出版社（假設外鍵必有對應資料）
                    CategoryId = l.CategoryID,
                    CategoryName = l.Category!.CategoryName,

                    PublisherId = l.PublisherID,
                    PublisherName = l.Publisher!.PublisherName,

                    // 主作者（或第一位作者；若集合為空會丟例外，方便提早發現異常資料）
                    AuthorName = l.ListingAuthors
                        .OrderByDescending(a => a.IsPrimary)
                        .Select(a => a.AuthorName)
                        .First(),   // ← 確保一定有值

                    // 明細中需要列出所有作者 / 圖片
                    ListingAuthors = l.ListingAuthors.ToList(),
                    ListingImages = l.ListingImages.ToList(),

                    // 封面圖（可依你的規則改排序）
                    ImageUrl = l.ListingImages
                        .OrderByDescending(i => i.ImageID)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();

            // 若要在同一個 partial 內切換到「編輯表單」，這裡先準備下拉選項
            ViewBag.CategoryID = new SelectList(
                await _context.Categories.AsNoTracking().ToListAsync(),
                "CategoryID", "CategoryName", vm.CategoryId);

            ViewBag.PublisherID = new SelectList(
                await _context.Publishers.AsNoTracking().ToListAsync(),
                "PublisherID", "PublisherName", vm.PublisherId);

            return PartialView("_Detail", vm);
        }



        // GET: Listings/Create
        public async Task<IActionResult> Create()
        {
            var listingC = new ListingsViewModel
            {
                ImageSource = "cloud"
            };
          
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName");
            ViewData["PublisherID"] = new SelectList(_context.Publishers, "PublisherID", "PublisherName");
            return View(listingC);
        }

        // POST: Listings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]      
        public async Task<IActionResult> Create(ListingsViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // 回填下拉框,防止驗證失敗後下拉框無法選擇
                ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                ViewData["PublisherID"] = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);
                return View(vm);
            }

            // 確保作者必填（Server-side 再驗證一次）
            if (string.IsNullOrWhiteSpace(vm.AuthorName))
            {
                ModelState.AddModelError(nameof(vm.AuthorName), "請輸入作者");
                ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                ViewData["PublisherID"] = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);
                return View(vm);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) 建立 Listing
                var listing = new Listing
                {
                    CategoryID = vm.CategoryId,
                    PublisherID = vm.PublisherId,
                    Title = vm.Title,
                    ISBN = vm.ISBN,
                    Condition = vm.Condition,
                    Status = 0,
                    CreatedAt = DateTime.Now
                };
                _context.Listings.Add(listing);
                await _context.SaveChangesAsync(); // 取得 ListingID

                // 2) 建立 1 筆 ListingAuthor（IsPrimary = true）
                var author = new ListingAuthor
                {
                    ListingID = listing.ListingID,
                    AuthorName = vm.AuthorName.Trim(),
                    IsPrimary = true,
                    CreatedAt = DateTime.Now, // 或 DateTime.UtcNow，看你專案慣例
                };
                _context.ListingAuthors.Add(author);

                // 3) 視需要建立 ListingImage（有圖才建）
                string? imageUrl = null;
                switch ((vm.ImageSource ?? "none").ToLowerInvariant())
                {
                    case "cloud":
                        if (!string.IsNullOrWhiteSpace(vm.CloudImageUrl))
                            imageUrl = vm.CloudImageUrl!.Trim();
                        break;

                    case "local":
                        if (vm.LocalImage != null && vm.LocalImage.Length > 0)
                        {
                            var saved = await SaveLocalImageAsync(vm.LocalImage); // 下方提供
                            if (!saved.Success)
                            {
                                ModelState.AddModelError(nameof(vm.LocalImage), saved.ErrorMessage ?? "圖片上傳失敗");
                                // 回填下拉
                                ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                                ViewData["PublisherID"] = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);
                                return View(vm);
                            }
                            imageUrl = saved.RelativeUrl!;
                        }
                        break;

                    case "none":
                    default:
                        break; // 不建任何 ListingImage
                }

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var listingImage = new ListingImage
                    {
                        ListingID = listing.ListingID,
                        ImageUrl = imageUrl,          // 可為 /uploads/... 或 https://...
                        Caption = "封面",             // 依你需求固定為封面
                        CreatedAt = DateTime.Now,
                    };
                    _context.ListingImages.Add(listingImage);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                // 簡單回報錯誤（實務可記 Log）
                ModelState.AddModelError(string.Empty, "建立失敗，請稍後再試");
                ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                ViewData["PublisherID"] = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);
                return View(vm);
            }
        }






        // 處理本地圖片上傳
        private async Task<(bool Success, string? RelativeUrl, string? ErrorMessage)> SaveLocalImageAsync(IFormFile file)
        {
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
                return (false, null, "不支援的圖片格式");

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return (false, null, "Content-Type 非 image/*");

            const long maxBytes = 5 * 1024 * 1024;
            if (file.Length > maxBytes)
                return (false, null, "圖片太大，超過 5MB");

            var folder = Path.Combine(_env.WebRootPath, "uploads", "books");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var physical = Path.Combine(folder, fileName);

            using (var stream = System.IO.File.Create(physical))
            {
                await file.CopyToAsync(stream);
            }

            var relative = $"/uploads/books/{fileName}";
            return (true, relative, null);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditInline(ListingseditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CategoryID = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                ViewBag.PublisherID = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);


                var detailVm = await MapToDetailVmAsync(vm);
                return PartialView("_Detail", detailVm);
            }

            var listing = await _context.Listings
                .Include(l => l.ListingAuthors)
                .Include(l => l.ListingImages)
                .FirstOrDefaultAsync(l => l.ListingID == vm.ListingId);

            if (listing == null) return NotFound();

            // 1) 更新基本欄位
            listing.Title = vm.Title?.Trim();
            listing.CategoryID= vm.CategoryId;
            listing.PublisherID = vm.PublisherId;
            listing.Condition = vm.Condition;
            listing.Status = vm.Status;
            listing.ISBN = vm.ISBN;

            // 2) 主作者（有則改名，無則新增）
            var author = listing.ListingAuthors.FirstOrDefault(a => a.IsPrimary);
            if (author == null)
            {
                author = new ListingAuthor
                {
                    ListingID = listing.ListingID,
                    IsPrimary = true,
                    CreatedAt = DateTime.Now
                };
                _context.ListingAuthors.Add(author);
            }
            author.AuthorName = (vm.AuthorName ?? string.Empty).Trim();

            // 3) 圖片：決定要不要更新封面
            string? finalImageUrl = null;

            if (string.Equals(vm.ImageSource, "cloud", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(vm.CloudImageUrl))
                    finalImageUrl = vm.CloudImageUrl.Trim();
            }
            else if (string.Equals(vm.ImageSource, "local", StringComparison.OrdinalIgnoreCase)
                     && vm.LocalImage is { Length: > 0 })
            {
                // 儲存本機上傳 → 回相對網址 /uploads/books/xxx.ext
                var saved = await SaveLocalImageAsync(vm.LocalImage);
                if (!saved.Success)
                {
                    ModelState.AddModelError(nameof(vm.LocalImage), saved.ErrorMessage ?? "圖片上傳失敗");
                    ViewBag.CategoryID = new SelectList(_context.Categories, "CategoryID", "CategoryName", vm.CategoryId);
                    ViewBag.PublisherID = new SelectList(_context.Publishers, "PublisherID", "PublisherName", vm.PublisherId);
                    return PartialView("_Detail", vm);
                }
                finalImageUrl = saved.RelativeUrl!;
            }
            else if (string.Equals(vm.ImageSource, "none", StringComparison.OrdinalIgnoreCase))
            {
                // 不變更圖片 → 不做任何事
            }

            // 4) 寫回封面：有新圖就更新或新增一筆
            if (!string.IsNullOrEmpty(finalImageUrl))
            {
                // 你也可以改成用 Caption == "封面" 來定位
                var cover = listing.ListingImages
                                   .OrderByDescending(i => i.ImageID)
                                   .FirstOrDefault();

                if (cover == null)
                {
                    listing.ListingImages.Add(new ListingImage
                    {
                        ImageUrl = finalImageUrl,
                        Caption = "封面",
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    cover.ImageUrl = finalImageUrl;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(); 
        }
        // 顯示用補足名稱等資訊
        private async Task<ListingsdetailViewModel> MapToDetailVmAsync(ListingseditViewModel vm)
        {
            
            var categoryName = await _context.Categories
                .Where(c => c.CategoryID == vm.CategoryId)
                .Select(c => c.CategoryName)
                .FirstOrDefaultAsync();

            string? publisherName = null;
            if (vm.PublisherId != 0)
            {
                publisherName = await _context.Publishers
                    .Where(p => p.PublisherID == vm.PublisherId)
                    .Select(p => p.PublisherName)
                    .FirstOrDefaultAsync();
            }

            // 最新封面
            var imageUrl = await _context.ListingImages
                .Where(i => i.ListingID == vm.ListingId)
                .OrderByDescending(i => i.ImageID)
                .Select(i => i.ImageUrl)
                .FirstOrDefaultAsync();

            // 全作者清單（顯示區會用到）
            var authors = await _context.ListingAuthors
                .Where(a => a.ListingID == vm.ListingId)
                .ToListAsync();

            var images = await _context.ListingImages
                .Where(i => i.ListingID == vm.ListingId)
                .ToListAsync();

            return new ListingsdetailViewModel
            {
                ListingId = vm.ListingId,
                Title = vm.Title,
                ISBN = vm.ISBN,
                Condition = vm.Condition,
                Status = vm.Status,

                CategoryId = vm.CategoryId,
                CategoryName = categoryName ?? "",

                PublisherId = vm.PublisherId,
                PublisherName = publisherName ?? "",

                AuthorName = vm.AuthorName,
                ListingAuthors = authors,
                ListingImages = images,
                ImageUrl = imageUrl,

                // 保留圖片來源欄位（若你的 detail VM 有）
                ImageSource = vm.ImageSource,
                CloudImageUrl = vm.CloudImageUrl,
                LocalImage = vm.LocalImage
            };
        }









        // GET: Listings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var listing = await _context.Listings.FindAsync(id);
            if (listing == null)
            {
                return NotFound();
            }
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", listing.CategoryID);
            return View(listing);
        }

        // POST: Listings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ListingID,CategoryID,PublisherID,Title,ISBN,Condition,CreatedAt,Status,IsAvailable")] Listing listing)
        {
            if (id != listing.ListingID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(listing);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", listing.CategoryID);
            return View(listing);
        }

   
    }
}
