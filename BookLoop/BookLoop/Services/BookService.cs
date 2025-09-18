using BookLoop.Data;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Services
{
	public class BookService
	{
		private readonly BookSystemContext _context;

		public BookService(BookSystemContext context)
		{
			_context = context;
		}

		// 取得書籍清單
		public async Task<List<Book>> GetBooksAsync()
		{
			return await _context.Books
				.Include(b => b.Publisher)
				.Include(b => b.Category)
				.OrderBy(b => b.Title)
				.Take(100)
				.ToListAsync();
		}

		// 依 ID 取得單一本書
		public async Task<Book?> GetBookByIdAsync(int id)
		{
			return await _context.Books.FindAsync(id);
		}

		// 依 ID 取得書籍（含關聯資料）
		public async Task<Book?> GetBookWithRelationsAsync(int id)
		{
			return await _context.Books
				.Include(b => b.Publisher)
				.Include(b => b.Category)
				.FirstOrDefaultAsync(m => m.BookID == id);
		}

		// 新增書籍
		public async Task AddBookAsync(Book book)
		{
			if (string.IsNullOrWhiteSpace(book.Slug))
			{
				book.Slug = !string.IsNullOrWhiteSpace(book.ISBN)
					? book.ISBN.ToLower()
					: Guid.NewGuid().ToString("N");
			}

			if (book.PublishDate == null)
				book.PublishDate = DateTime.Now;

			if (book.CreatedAt == default)
				book.CreatedAt = DateTime.Now;

			book.UpdatedAt = DateTime.Now;
			book.IsDeleted = false;

			_context.Add(book);
			await _context.SaveChangesAsync();
		}

		// 更新書籍
		public async Task UpdateBookAsync(Book book)
		{
			if (!string.IsNullOrWhiteSpace(book.Title))
				book.Slug = book.Title.Replace(" ", "-").ToLower();
			else if (!string.IsNullOrWhiteSpace(book.ISBN))
				book.Slug = book.ISBN.ToLower();
			else
				book.Slug = Guid.NewGuid().ToString("N");

			book.UpdatedAt = DateTime.Now;

			try
			{
				_context.Update(book);
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!await _context.Books.AnyAsync(e => e.BookID == book.BookID))
					throw new KeyNotFoundException($"找不到 BookID={book.BookID} 的書籍。");
				else
					throw;
			}
		}

		// 刪除書籍
		public async Task DeleteBookAsync(int id)
		{
			var book = await _context.Books.FindAsync(id);
			if (book != null)
			{
				_context.Books.Remove(book); // 如果要「軟刪除」改成 book.IsDeleted = true;
				await _context.SaveChangesAsync();
			}
		}
	}
}
