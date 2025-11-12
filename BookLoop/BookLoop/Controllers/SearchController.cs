using BookLoop.Data;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Controllers
{
	public class SearchController : Controller
	{
		private readonly BookSystemContext? _bookDb;
		private readonly OrdersysContext? _orderDb;
		private readonly MemberContext? _memberDb;
		private readonly BorrowContext? _borrowDb;
		private readonly ReportMailDbContext? _mailDb;

		public SearchController(IServiceProvider sp)
		{
			_bookDb = sp.GetService<BookSystemContext>();
			_orderDb = sp.GetService<OrdersysContext>();
			_memberDb = sp.GetService<MemberContext>();
			_borrowDb = sp.GetService<BorrowContext>();
			_mailDb = sp.GetService<ReportMailDbContext>();
		}

		[HttpGet]
		public async Task<IActionResult> Index(string? q)
		{
			q = (q ?? "").Trim();
			var vm = new SearchResultVm { Query = q };
			if (string.IsNullOrWhiteSpace(q)) return View(vm);

			var tasks = new List<Task>();

			// 📚 書籍
			if (_bookDb != null)
			{
				tasks.Add(Task.Run(async () =>
				{
					var list = await _bookDb.Books
						.Include(b => b.Publisher)
						.Include(b => b.Category)
						.Where(b =>
							b.Title.Contains(q) ||
							(b.ISBN ?? "").Contains(q) ||
							(b.Publisher != null && b.Publisher.PublisherName.Contains(q)) ||
							(b.Category != null && b.Category.CategoryName.Contains(q)))
						.OrderByDescending(b => b.UpdatedAt)
						.Take(10)
						.ToListAsync();

					lock (vm)
					{
						foreach (var b in list)
						{
							vm.Hits.Add(new SearchHit
							{
								Type = "Book",
								Title = b.Title,
								Sub = $"ISBN {b.ISBN}｜{b.Publisher?.PublisherName}｜{b.Category?.CategoryName}",
								Url = Url.Action("Details", "Books", new { area = "Books", id = b.BookID })!
							});
						}
					}
				}));
			}

			// 🛒 訂單
			if (_orderDb != null)
			{
				tasks.Add(Task.Run(async () =>
				{
					var orders = await _orderDb.Orders
						.Where(o => o.OrderID.ToString().Contains(q) ||
								   (o.CouponNameSnap ?? "").Contains(q) ||
								   (o.Customer != null && o.Customer.CustomerID.ToString().Contains(q)))
						.OrderByDescending(o => o.CreatedAt)
						.Take(10)
						.ToListAsync();

					lock (vm)
					{
						foreach (var o in orders)
						{
							vm.Hits.Add(new SearchHit
							{
								Type = "Order",
								Title = $"訂單 #{o.OrderID}",
								Sub = $"金額：NT$ {o.TotalAmount:n0}｜狀態代碼：{o.Status}",
								Url = Url.Action("Details", "Orders", new { area = "Orders", id = o.OrderID })!
							});
						}
					}
				}));
			}

			// 👤 會員
			if (_memberDb != null)
			{
				tasks.Add(Task.Run(async () =>
				{
					var members = await _memberDb.Members
						.Where(m => (m.Username ?? "").Contains(q) ||
									(m.Email ?? "").Contains(q) ||
									(m.Phone ?? "").Contains(q))
						.OrderByDescending(m => m.UpdatedAt)
						.Take(10)
						.ToListAsync();

					lock (vm)
					{
						foreach (var m in members)
						{
							vm.Hits.Add(new SearchHit
							{
								Type = "Member",
								Title = m.Username,
								Sub = $"{m.Email}｜{m.Phone}",
								Url = Url.Action("Details", "Members", new { area = "Account", id = m.MemberID })!
							});
						}
					}
				}));
			}

			// 📖 借閱紀錄
			if (_borrowDb != null)
			{
				tasks.Add(Task.Run(async () =>
				{
					var records = await _borrowDb.BorrowRecords
						.Include(b => b.Member)
						.Include(b => b.Listing)
						.Where(b =>
							(b.Member != null && b.Member.Username.Contains(q)) ||
							(b.Listing != null && b.Listing.Title.Contains(q)))
						.OrderByDescending(b => b.UpdatedAt)
						.Take(10)
						.ToListAsync();

					lock (vm)
					{
						foreach (var r in records)
						{
							vm.Hits.Add(new SearchHit
							{
								Type = "BorrowRecord",
								Title = r.Listing?.Title ?? "(未命名書籍)",
								Sub = $"會員：{r.Member?.Username}｜狀態：{r.StatusName}",
								Url = Url.Action("Edit", "BorrowRecords", new { area = "Borrows", id = r.RecordID })!
							});
						}
					}
				}));
			}

			// ✉️ 郵件紀錄
			//if (_mailDb != null)
			//{
			//	tasks.Add(Task.Run(async () =>
			//	{
			//		var mails = await _mailDb.ReportMails
			//			.Where(m => m.Subject.Contains(q) ||
			//						(m.RecipientEmail ?? "").Contains(q))
			//			.OrderByDescending(m => m.SentAt)
			//			.Take(10)
			//			.ToListAsync();

			//		lock (vm)
			//		{
			//			foreach (var m in mails)
			//			{
			//				vm.Hits.Add(new SearchHit
			//				{
			//					Type = "Mail",
			//					Title = m.Subject,
			//					Sub = $"{m.RecipientEmail}｜發送時間：{m.SentAt:yyyy/MM/dd HH:mm}",
			//					Url = Url.Action("Details", "ReportMails", new { area = "Reports", id = m.ReportMailID })!
			//				});
			//			}
			//		}
			//	}));
			//}

			await Task.WhenAll(tasks);
			if (vm.Hits.Count == 1)
				return Redirect(vm.Hits[0].Url);

			return View(vm);
		}
	}
}
