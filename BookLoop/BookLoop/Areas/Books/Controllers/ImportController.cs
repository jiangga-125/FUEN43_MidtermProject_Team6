using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using BookLoop.Models.Dto;
using BookLoop.Services.Import;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BookSystem.Controllers
{
	[Area("Books")] // 匯入功能放在 Books 區域
	public class ImportController : Controller
	{
		private readonly BookSystemContext _context;

		public ImportController(BookSystemContext context)
		{
			_context = context;
		}

		#region 匯入分類 (原有功能)

		// GET: /Books/Import
		// 顯示上傳頁面
		public IActionResult Index()
		{
			return View();
		}

		// POST: /Books/Import
		// 上傳 CSV/JSON 並預覽分類資料
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Index(IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				ViewBag.Error = "請選擇一個檔案";
				return View();
			}

			var previewList = new List<ImportCategoryDto>();
			var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

			using (var stream = file.OpenReadStream())
			using (var reader = new StreamReader(stream))
			{
				if (ext == ".csv")
				{
					try
					{
						using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
						previewList = csv.GetRecords<ImportCategoryDto>()
										 .Take(10) // 只取前 10 筆預覽
										 .ToList();
					}
					catch (Exception ex)
					{
						ViewBag.Error = $"CSV 解析失敗：{ex.Message}";
						return View();
					}
				}
				else if (ext == ".json")
				{
					try
					{
						var json = await reader.ReadToEndAsync();
						previewList = System.Text.Json.JsonSerializer
									   .Deserialize<List<ImportCategoryDto>>(json)
									   ?.Take(10)
									   .ToList() ?? new List<ImportCategoryDto>();
					}
					catch (Exception ex)
					{
						ViewBag.Error = $"JSON 解析失敗：{ex.Message}";
						return View();
					}
				}
				else
				{
					ViewBag.Error = "僅支援 CSV 或 JSON 檔案";
					return View();
				}
			}

			// 顯示預覽畫面
			return View("Preview", previewList);
		}

		// POST: /Books/Import/Commit
		// 確認匯入分類 → 寫入資料庫
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Commit(List<ImportCategoryDto> categories)
		{
			if (categories == null || !categories.Any())
			{
				TempData["Error"] = "沒有可匯入的分類資料";
				return RedirectToAction("Index");
			}

			int inserted = 0, skipped = 0;

			foreach (var dto in categories)
			{
				if (string.IsNullOrWhiteSpace(dto.CategoryName))
				{
					skipped++;
					continue;
				}

				var slug = SlugHelper.Generate(dto.CategoryName);

				// 確認是否已有相同分類
				bool exists = _context.Categories.Any(x =>
					x.CategoryName == dto.CategoryName || x.Slug == slug);

				if (!exists)
				{
					var c = new Category
					{
						CategoryName = dto.CategoryName,
						Slug = slug,
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow,
						IsDeleted = false
					};
					_context.Categories.Add(c);
					inserted++;
				}
				else
				{
					skipped++;
				}
			}

			await _context.SaveChangesAsync();

			TempData["Success"] = $"分類匯入完成！新增 {inserted} 筆，跳過 {skipped} 筆（重複或不合法）";
			return RedirectToAction("Index", "Categories");
		}

		#endregion

		#region 匯入書籍 (預覽)

		//上傳書籍 CSV/JSON
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> IndexBooks(IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				ViewBag.Error = "請選擇一個檔案";
				return View("Index");
			}

			var previewList = new List<ImportBookDto>();
			var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

			using (var stream = file.OpenReadStream())
			using (var reader = new StreamReader(stream))
			{
				if (ext == ".csv")
				{
					try
					{
						using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
						// 取全部，讓使用者在預覽頁面勾選要匯入的
						previewList = csv.GetRecords<ImportBookDto>().ToList();
					}
					catch (Exception ex)
					{
						ViewBag.Error = $"CSV 解析失敗：{ex.Message}";
						return View("Index");
					}
				}
				else if (ext == ".json")
				{
					try
					{
						var json = await reader.ReadToEndAsync();

						using var doc = System.Text.Json.JsonDocument.Parse(json); // 檢查格式
						var rawList = System.Text.Json.JsonSerializer
										.Deserialize<List<Dictionary<string, object>>>(json);

						if (rawList == null || rawList.Count == 0)
						{
							ViewBag.Error = "JSON 解析失敗：沒有資料";
							return View("Index");
						}

						var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "ISBN", "ISBN" },
					{ "書名", "Title" }, { "書名(正題名)", "Title" }, { "Title", "Title" },
					{ "作者", "Author" }, { "Author", "Author" },
					{ "出版者", "Publisher" }, { "出版社", "Publisher" }, { "Publisher", "Publisher" },
					{ "出版日期", "PublishDate" }, { "PublishDate", "PublishDate" },
					{ "主題", "Category" }, { "分類", "Category" }, { "Category", "Category" },
					{ "ImagePath", "ImagePath" }, { "圖片", "ImagePath" }
				};

						previewList = rawList.Select(x =>
						{
							var dto = new ImportBookDto();
							foreach (var kv in x)
							{
								if (fieldMap.TryGetValue(kv.Key, out var propName))
								{
									var prop = typeof(ImportBookDto).GetProperty(propName);
									prop?.SetValue(dto, kv.Value?.ToString());
								}
							}
							return dto;
						})
						.ToList(); // 取全部
					}
					catch (Exception ex)
					{
						ViewBag.Error = $"JSON 解析失敗：{ex.Message}";
						return View("Index");
					}
				}
				else
				{
					ViewBag.Error = "僅支援 CSV 或 JSON 檔案";
					return View("Index");
				}
			}

			// 顯示整份預覽清單（使用者在畫面上勾選要匯入的）
			return View("PreviewBooks", previewList);
		}


		#endregion

		#region 匯入書籍 (確認匯入)

		/// <summary>
		/// 確認匯入書籍 → 寫入資料庫
		/// </summary>
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CommitBooks(List<ImportBookDto> books)
		{
			if (books == null || !books.Any())
			{
				TempData["Error"] = "沒有可匯入的書籍資料";
				return RedirectToAction("Index");
			}

			// 只匯入使用者勾選的；若沒有勾任何筆就預設匯入全部
			var toImport = books.Where(b => b.Selected).ToList();
			if (!toImport.Any()) toImport = books;

			int inserted = 0, updated = 0;

			// 使用 transaction，較安全
			using var transaction = await _context.Database.BeginTransactionAsync();

			// 先把現有資料 load 成字典以便 cache（大小視資料量調整）
			var publishersDict = await _context.Publishers
				.AsNoTracking()
				.ToDictionaryAsync(p => p.PublisherName, StringComparer.OrdinalIgnoreCase);
			var categoriesDict = await _context.Categories
				.AsNoTracking()
				.ToDictionaryAsync(c => c.CategoryName, StringComparer.OrdinalIgnoreCase);
			var authorsDict = await _context.Authors
				.AsNoTracking()
				.ToDictionaryAsync(a => a.AuthorName, StringComparer.OrdinalIgnoreCase);

			// 暫存要 later 處理的作者與圖片（因為需要 book.BookID）
			var pendingAuthors = new List<(string isbn, string authorName)>();
			var pendingImages = new List<(string isbn, string imagePath)>();

			// 先處理 publishers/categories & book 新增/更新（不立即 SaveChanges）
			foreach (var dto in toImport)
			{
				if (string.IsNullOrWhiteSpace(dto.ISBN) || string.IsNullOrWhiteSpace(dto.Title))
					continue;

				string cleanIsbn = dto.ISBN.Replace("-", "").Trim();

				// 找舊書（用 ISBN）
				var book = await _context.Books.FirstOrDefaultAsync(x => x.ISBN == cleanIsbn);

				// 處理出版社（cache lookup，沒則新增到 context 並加到 dict）
				Publisher? publisher = null;
				if (!string.IsNullOrWhiteSpace(dto.Publisher))
				{
					if (publishersDict.TryGetValue(dto.Publisher, out var p))
					{
						publisher = p;
					}
					else
					{
						publisher = new Publisher
						{
							PublisherName = dto.Publisher,
							Slug = SlugHelper.Generate(dto.Publisher),
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow,
							IsDeleted = false
						};
						_context.Publishers.Add(publisher);
						// 加到 dict 以便後續 reuse (注意：尚未有 ID，會在 SaveChanges 後取得)
						publishersDict[dto.Publisher] = publisher;
					}
				}

				// 處理分類
				Category? category = null;
				if (!string.IsNullOrWhiteSpace(dto.Category))
				{
					if (categoriesDict.TryGetValue(dto.Category, out var c))
					{
						category = c;
					}
					else
					{
						category = new Category
						{
							CategoryName = dto.Category,
							Slug = SlugHelper.Generate(dto.Category),
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow,
							IsDeleted = false
						};
						_context.Categories.Add(category);
						categoriesDict[dto.Category] = category;
					}
				}

				// 解析出版日（允許多格式）
				DateTime? publishDate = null;
				if (!string.IsNullOrWhiteSpace(dto.PublishDate))
				{
					string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy/M/d", "yyyy/M/dd", "yyyy-M-d" };
					if (DateTime.TryParseExact(dto.PublishDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
					{
						publishDate = parsed;
					}
					else if (DateTime.TryParse(dto.PublishDate, out var parsed2))
					{
						publishDate = parsed2;
					}
				}
				if (publishDate == null) publishDate = DateTime.UtcNow;

				if (book != null)
				{
					// 更新欄位（排除 ISBN，本範例僅更新 Title/PublishDate 與關聯 ID）
					var oldTitle = book.Title;
					if (!string.IsNullOrWhiteSpace(dto.Title)) book.Title = dto.Title;
					if (publishDate.HasValue) book.PublishDate = publishDate.Value;
					book.PublisherID = publisher?.PublisherID ?? book.PublisherID;
					book.CategoryID = category?.CategoryID ?? book.CategoryID;

					if (!string.Equals(oldTitle, book.Title, StringComparison.OrdinalIgnoreCase))
					{
						book.Slug = SlugHelper.Generate(book.Title);
					}

					book.IsDeleted = false;
					book.UpdatedAt = DateTime.UtcNow;

					updated++;
				}
				else
				{
					// 新增 book（PublisherID/CategoryID 目前可能還沒 ID，稍後 SaveChanges 會補）
					var newBook = new Book
					{
						ISBN = cleanIsbn,
						Title = dto.Title,
						PublishDate = publishDate.Value,
						Slug = SlugHelper.Generate(dto.Title),
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow,
						IsDeleted = false
					};

					// 若 publisher/category 已存在且有 ID -> 指定 ID，否則稍後透過 navigation object 關聯
					if (publisher != null && publisher.PublisherID > 0) newBook.PublisherID = publisher.PublisherID;
					if (category != null && category.CategoryID > 0) newBook.CategoryID = category.CategoryID;

					// 如果 publisher/category 還沒 ID（剛加入 context），我們也可以用 navigation
					if (publisher != null && publisher.PublisherID == 0) newBook.Publisher = publisher;
					if (category != null && category.CategoryID == 0) newBook.Category = category;

					_context.Books.Add(newBook);
					inserted++;
				}

				// 暫存作者與圖片（後處理）
				if (!string.IsNullOrWhiteSpace(dto.Author))
					pendingAuthors.Add((cleanIsbn, dto.Author));

				if (!string.IsNullOrWhiteSpace(dto.ImagePath))
					pendingImages.Add((cleanIsbn, dto.ImagePath));
			}

			// 第一次儲存：把 new publishers/categories/books 寫入 DB，取得 ID
			await _context.SaveChangesAsync();

			// 重新 refresh authorsDict（避免新增時再查）
			authorsDict = await _context.Authors
				.AsNoTracking()
				.ToDictionaryAsync(a => a.AuthorName, StringComparer.OrdinalIgnoreCase);

			// 處理 BookAuthors & BookImages
			foreach (var (isbn, authorName) in pendingAuthors)
			{
				var book = await _context.Books.FirstOrDefaultAsync(b => b.ISBN == isbn);
				if (book == null) continue;

				if (!authorsDict.TryGetValue(authorName, out var author))
				{
					author = new Author
					{
						AuthorName = authorName,
						Slug = SlugHelper.Generate(authorName),
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow,
						IsDeleted = false
					};
					_context.Authors.Add(author);
					// 加到字典（ID 尚未有，稍後 SaveChanges 後會有）
					authorsDict[authorName] = author;
				}

				// 確認關聯是否已存在
				bool hasRelation = await _context.BookAuthors
					.AnyAsync(ba => ba.BookID == book.BookID && ba.AuthorID == author.AuthorID);

				if (!hasRelation)
				{
					_context.BookAuthors.Add(new BookAuthor
					{
						BookID = book.BookID,
						AuthorID = author.AuthorID, // 若 author 尚未有 ID EF 會處理（必須 SaveChanges）
						AuthorOrder = 1
					});
				}
			}

			foreach (var (isbn, imagePath) in pendingImages)
			{
				var book = await _context.Books.FirstOrDefaultAsync(b => b.ISBN == isbn);
				if (book == null) continue;

				bool hasImage = await _context.BookImages
					.AnyAsync(img => img.BookID == book.BookID && img.IsPrimary);

				if (!hasImage)
				{
					_context.BookImages.Add(new BookImage
					{
						BookID = book.BookID,
						FilePath = imagePath,
						IsPrimary = true,
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow
					});
				}
			}

			// 最後一次 SaveChanges 並 commit
			await _context.SaveChangesAsync();
			await transaction.CommitAsync();

			TempData["Success"] = $"書籍匯入完成：新增 {inserted} 筆，更新 {updated} 筆。";
			return RedirectToAction("Index", "Books");
		}



		#endregion
	}
}
