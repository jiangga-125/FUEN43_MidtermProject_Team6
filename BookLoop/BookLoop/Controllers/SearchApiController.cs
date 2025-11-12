using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers;

[ApiController]
[Route("api/search")]
public class SearchApiController : ControllerBase
{
	private readonly BookSystemContext? _bookDb;
	private readonly OrdersysContext? _orderDb;
	private readonly MemberContext? _memberDb;
	private readonly BorrowContext? _borrowDb;
	// private readonly ReportMailDbContext? _mailDb; // TODO

	public SearchApiController(IServiceProvider sp)
	{
		_bookDb = sp.GetService<BookSystemContext>();
		_orderDb = sp.GetService<OrdersysContext>();
		_memberDb = sp.GetService<MemberContext>();
		_borrowDb = sp.GetService<BorrowContext>();
		// _mailDb   = sp.GetService<ReportMailDbContext>();
	}

	// 下拉建議
	[HttpGet("suggest")]
	[AllowAnonymous]
	public async Task<IActionResult> Suggest([FromQuery] string q, [FromQuery] int take = 5)
	{
		q = (q ?? "").Trim();
		if (q.Length < 2) return Ok(new { query = q, items = Array.Empty<object>() });

		var list = new List<SearchHit>();
		var jobs = new List<Task>();

		if (_bookDb != null)
			jobs.Add(Task.Run(async () =>
			{
				var data = await _bookDb.Books
					.Where(b => b.Title.Contains(q) || (b.ISBN ?? "").Contains(q))
					.OrderByDescending(b => b.UpdatedAt).Take(take)
					.Select(b => new { b.BookID, b.Title, b.ISBN })
					.ToListAsync();
				lock (list)
					list.AddRange(data.Select(b => new SearchHit
					{
						Type = "Book",
						Title = b.Title,
						Sub = $"ISBN {b.ISBN}",
						Url = $"/Books/Books/Details/{b.BookID}"
					}));
			}));

		if (_orderDb != null)
			jobs.Add(Task.Run(async () =>
			{
				var data = await _orderDb.Orders
					.Where(o => o.OrderID.ToString().Contains(q))
					.OrderByDescending(o => o.CreatedAt).Take(take)
					.Select(o => new { o.OrderID, o.TotalAmount }).ToListAsync();
				lock (list)
					list.AddRange(data.Select(o => new SearchHit
					{
						Type = "Order",
						Title = $"訂單 #{o.OrderID}",
						Sub = $"NT$ {o.TotalAmount:n0}",
						Url = $"/Orders/Orders/Details/{o.OrderID}"
					}));
			}));

		if (_memberDb != null)
			jobs.Add(Task.Run(async () =>
			{
				var data = await _memberDb.Members
					.Where(m => (m.Username ?? "").Contains(q) || (m.Email ?? "").Contains(q))
					.OrderByDescending(m => m.UpdatedAt).Take(take)
					.Select(m => new { m.MemberID, m.Username, m.Email }).ToListAsync();
				lock (list)
					list.AddRange(data.Select(m => new SearchHit
					{
						Type = "Member",
						Title = m.Username,
						Sub = m.Email,
						Url = $"/Account/Members/Details/{m.MemberID}"
					}));
			}));

		if (_borrowDb != null)
			jobs.Add(Task.Run(async () =>
			{
				var data = await _borrowDb.BorrowRecords
					.Include(r => r.Member).Include(r => r.Listing)
					.Where(r => (r.Member != null && r.Member.Username.Contains(q)) ||
								(r.Listing != null && r.Listing.Title.Contains(q)))
					.OrderByDescending(r => r.UpdatedAt).Take(take)
					.Select(r => new { r.RecordID, Title = r.Listing.Title, Member = r.Member.Username })
					.ToListAsync();
				lock (list)
					list.AddRange(data.Select(r => new SearchHit
					{
						Type = "Borrows",
						Title = r.Title,
						Sub = $"會員：{r.Member}",
						Url = $"/Borrows/BorrowRecords/Details/{r.RecordID}"
					}));
			}));

		// TODO: ReportMail

		await Task.WhenAll(jobs);

		// 去重、裁切
		var items = list.Take(take).ToList();
		return Ok(new { query = q, items });
	}

	// 即時
	[HttpGet("quick")]
	[AllowAnonymous]
	public async Task<IActionResult> Quick([FromQuery] string q, [FromQuery] int takePerSource = 6)
	{
		q = (q ?? "").Trim();
		if (q.Length < 2) return Ok(new { query = q, items = Array.Empty<SearchHit>() });

		var all = new List<SearchHit>();
		var tasks = new List<Task>();

		tasks.Add(SuggestFromBooks(q, takePerSource, all));
		tasks.Add(SuggestFromOrders(q, takePerSource, all));
		tasks.Add(SuggestFromMembers(q, takePerSource, all));
		tasks.Add(SuggestFromBorrow(q, takePerSource, all));
		// tasks.Add(SuggestFromMail(q, takePerSource, all)); // TODO

		await Task.WhenAll(tasks);
		return Ok(new { query = q, items = all });
	}

	// --- 內部 helper（避免重複） ---
	private Task SuggestFromBooks(string q, int take, List<SearchHit> sink) => _bookDb == null ? Task.CompletedTask : Task.Run(async () =>
	{
		var data = await _bookDb.Books
			.Include(b => b.Publisher).Include(b => b.Category)
			.Where(b => b.Title.Contains(q) || (b.ISBN ?? "").Contains(q))
			.OrderByDescending(b => b.UpdatedAt).Take(take)
			.Select(b => new { b.BookID, b.Title, b.ISBN, Pub = b.Publisher.PublisherName, Cat = b.Category.CategoryName })
			.ToListAsync();
		lock (sink)
			sink.AddRange(data.Select(b => new SearchHit
			{
				Type = "Book",
				Title = b.Title,
				Sub = $"ISBN {b.ISBN}｜{b.Pub}｜{b.Cat}",
				Url = $"/Books/Books/Details/{b.BookID}"
			}));
	});

	private Task SuggestFromOrders(string q, int take, List<SearchHit> sink) => _orderDb == null ? Task.CompletedTask : Task.Run(async () =>
	{
		var data = await _orderDb.Orders
			.Where(o => o.OrderID.ToString().Contains(q))
			.OrderByDescending(o => o.CreatedAt).Take(take)
			.Select(o => new { o.OrderID, o.TotalAmount, o.Status })
			.ToListAsync();
		lock (sink)
			sink.AddRange(data.Select(o => new SearchHit
			{
				Type = "Order",
				Title = $"訂單 #{o.OrderID}",
				Sub = $"NT$ {o.TotalAmount:n0}｜狀態代碼：{o.Status}",
				Url = $"/Orders/Orders/Details/{o.OrderID}"
			}));
	});

	private Task SuggestFromMembers(string q, int take, List<SearchHit> sink) => _memberDb == null ? Task.CompletedTask : Task.Run(async () =>
	{
		var data = await _memberDb.Members
			.Where(m => (m.Username ?? "").Contains(q) || (m.Email ?? "").Contains(q) || (m.Phone ?? "").Contains(q))
			.OrderByDescending(m => m.UpdatedAt).Take(take)
			.Select(m => new { m.MemberID, m.Username, m.Email, m.Phone })
			.ToListAsync();
		lock (sink)
			sink.AddRange(data.Select(m => new SearchHit
			{
				Type = "Member",
				Title = m.Username,
				Sub = $"{m.Email}｜{m.Phone}",
				Url = $"/Account/Members/Details/{m.MemberID}"
			}));
	});

	private Task SuggestFromBorrow(string q, int take, List<SearchHit> sink) => _borrowDb == null ? Task.CompletedTask : Task.Run(async () =>
	{
		var data = await _borrowDb.BorrowRecords
			.Include(r => r.Member).Include(r => r.Listing)
			.Where(r => (r.Member != null && r.Member.Username.Contains(q)) ||
						(r.Listing != null && r.Listing.Title.Contains(q)))
			.OrderByDescending(r => r.UpdatedAt).Take(take)
			.Select(r => new { r.RecordID, Title = r.Listing.Title, Member = r.Member.Username })
			.ToListAsync();
		lock (sink)
			sink.AddRange(data.Select(r => new SearchHit
			{
				Type = "Borrows",
				Title = r.Title,
				Sub = $"會員：{r.Member}",
				Url = $"/Borrows/BorrowRecords/Details/{r.RecordID}"
			}));
	});
}
