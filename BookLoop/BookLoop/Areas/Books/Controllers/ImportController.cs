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

		/// <summary>
		/// 上傳書籍 CSV/JSON，預覽前 10 筆
		/// </summary>
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
						previewList = csv.GetRecords<ImportBookDto>()
										 .Take(10)
										 .ToList();
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

						// 先檢查 JSON 格式是否正確
						try
						{
							using var doc = System.Text.Json.JsonDocument.Parse(json);
						}
						catch (System.Text.Json.JsonException jex)
						{
							ViewBag.Error = $"JSON 格式錯誤：{jex.Message}";
							return View("Index");
						}

						// 格式正確再反序列化
						var rawList = System.Text.Json.JsonSerializer
										.Deserialize<List<Dictionary<string, object>>>(json);

						if (rawList == null || rawList.Count == 0)
						{
							ViewBag.Error = "JSON 解析失敗：沒有資料";
							return View("Index");
						}

						// 欄位對照表：不同 JSON 欄位名稱 → ImportBookDto 屬性
						var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
							{
								{ "ISBN", "ISBN" },

								{ "書名", "Title" }, { "書名(正題名)", "Title" }, { "Title", "Title" },

								{ "作者", "Author" }, { "Author", "Author" },

								{ "出版者", "Publisher" }, { "出版社", "Publisher" }, { "Publisher", "Publisher" },

								{ "出版日期", "PublishDate" }, { "PublishDate", "PublishDate" },

								{ "主題", "Category" }, { "分類", "Category" }, { "Category", "Category" }
							};

						// 把 Dictionary 轉成 ImportBookDto
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
						.Take(10)
						.ToList();
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

			// 顯示書籍預覽畫面
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

			int inserted = 0, updated = 0;

			foreach (var dto in books)
			{
				if (string.IsNullOrWhiteSpace(dto.ISBN) || string.IsNullOrWhiteSpace(dto.Title))
					continue;

				string cleanIsbn = dto.ISBN.Replace("-", "").Trim();

				// 嘗試找舊書籍
				var book = await _context.Books
					.FirstOrDefaultAsync(x => x.ISBN == cleanIsbn);

				// 找或新增出版社
				Publisher? publisher = null;
				if (!string.IsNullOrWhiteSpace(dto.Publisher))
				{
					publisher = await _context.Publishers
						.FirstOrDefaultAsync(p => p.PublisherName == dto.Publisher);

					if (publisher == null)
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
						await _context.SaveChangesAsync();
					}
				}

				// 找或新增分類
				Category? category = null;
				if (!string.IsNullOrWhiteSpace(dto.Category))
				{
					category = await _context.Categories
						.FirstOrDefaultAsync(c => c.CategoryName == dto.Category);

					if (category == null)
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
						await _context.SaveChangesAsync();
					}
				}

				// 嘗試轉換出版日期（允許多格式）
				DateTime? publishDate = null;
				if (!string.IsNullOrWhiteSpace(dto.PublishDate))
				{
					string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy/M/d" };
					if (DateTime.TryParseExact(dto.PublishDate, formats,
						CultureInfo.InvariantCulture,
						DateTimeStyles.None,
						out var parsedDate))
					{
						publishDate = parsedDate;
					}
				}
				// 如果匯入資料沒有出版日 → 補今天
				if (publishDate == null)
				{
					publishDate = DateTime.UtcNow;
				}

				if (book != null)
				{
					// 更新模式（反射更新）
					string? oldTitle = book.Title;

					foreach (var prop in typeof(ImportBookDto).GetProperties())
					{
						if (prop.Name == "ISBN") continue; // ISBN 不更新

						var newValue = prop.GetValue(dto);
						if (newValue != null && !(newValue is string s && string.IsNullOrWhiteSpace(s)))
						{
							var targetProp = typeof(Book).GetProperty(prop.Name);
							if (targetProp != null && targetProp.CanWrite)
							{
								try
								{
									var converted = Convert.ChangeType(newValue, targetProp.PropertyType);
									targetProp.SetValue(book, converted);
								}
								catch
								{
									// 型別不符就跳過（Publisher, Category 等關聯另外處理）
								}
							}
						}
					}

					// 額外處理關聯與系統欄位
					book.PublisherID = publisher?.PublisherID ?? book.PublisherID;
					book.CategoryID = category?.CategoryID ?? book.CategoryID;
					if (publishDate.HasValue)
					{
						book.PublishDate = publishDate.Value;
					}

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
					// 新增模式
					book = new Book
					{
						ISBN = cleanIsbn,
						Title = dto.Title,
						PublisherID = (int)(publisher?.PublisherID),
						CategoryID = (int)(category?.CategoryID),
						PublishDate = publishDate,
						Slug = SlugHelper.Generate(dto.Title),
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow,
						IsDeleted = false
					};
					_context.Books.Add(book);
					inserted++;
				}

				await _context.SaveChangesAsync();

				// 作者處理
				if (!string.IsNullOrWhiteSpace(dto.Author))
				{
					var author = await _context.Authors.FirstOrDefaultAsync(a => a.AuthorName == dto.Author);
					if (author == null)
					{
						author = new Author
						{
							AuthorName = dto.Author,
							Slug = SlugHelper.Generate(dto.Author),
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow,
							IsDeleted = false
						};
						_context.Authors.Add(author);
						await _context.SaveChangesAsync();
					}

					bool hasRelation = await _context.BookAuthors
						.AnyAsync(ba => ba.BookID == book.BookID && ba.AuthorID == author.AuthorID);

					if (!hasRelation)
					{
						_context.BookAuthors.Add(new BookAuthor
						{
							BookID = book.BookID,
							AuthorID = author.AuthorID,
							AuthorOrder = 1
						});
						await _context.SaveChangesAsync();
					}
				}

				// 圖片處理
				if (!string.IsNullOrWhiteSpace(dto.ImagePath))
				{
					bool hasImage = await _context.BookImages
						.AnyAsync(img => img.BookID == book.BookID && img.IsPrimary);

					if (!hasImage)
					{
						_context.BookImages.Add(new BookImage
						{
							BookID = book.BookID,
							FilePath = dto.ImagePath,
							IsPrimary = true,
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow
						});
						await _context.SaveChangesAsync();
					}
				}
			}

			TempData["Success"] = $"書籍匯入完成：新增 {inserted} 筆，更新 {updated} 筆。";
			return RedirectToAction("Index", "Books");
		}


		#endregion
	}
}
