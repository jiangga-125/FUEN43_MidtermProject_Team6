using BookLoop.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Services;

public class MemberQueryService
{
	private readonly AppDbContext _db;
	public MemberQueryService(AppDbContext db) => _db = db;

	/// <summary>
	/// 取得分頁的會員清單，並帶出是否目前在黑名單（生效中）。
	/// </summary>
	public async Task<(IReadOnlyList<MemberListItem> Items, int Total)> GetPagedAsync(
		int page = 1, int pageSize = 20,
		string? keyword = null, int? status = null, bool? onlyBlacklisted = null)
	{
		var q = _db.Members.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(keyword))
		{
			var k = keyword.Trim();
			q = q.Where(m => m.Username.Contains(k) || (m.Email != null && m.Email.Contains(k)) || (m.Phone != null && m.Phone.Contains(k)));
		}
		if (status.HasValue)
		{
			q = q.Where(m => m.Status == status.Value);
		}

		// 目前黑名單（使用視圖 vw_BlacklistActive）
		var activeBlk = _db.Set<ActiveBlacklistView>().FromSqlRaw("SELECT * FROM dbo.vw_BlacklistActive");

		var joined = from m in q
					 join b in activeBlk on m.MemberID equals b.MemberID into bj
					 from b in bj.DefaultIfEmpty()
					 select new MemberListItem
					 {
						 MemberID = m.MemberID,
						 Username = m.Username,
						 Email = m.Email,
						 Phone = m.Phone,
						 Status = m.Status,
						 TotalBooks = m.TotalBooks,
						 TotalBorrows = m.TotalBorrows,
						 IsBlacklistedNow = b != null
					 };

		if (onlyBlacklisted == true) joined = joined.Where(x => x.IsBlacklistedNow);

		var total = await joined.CountAsync();
		var items = await joined
			.OrderByDescending(x => x.MemberID)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return (items, total);
	}

	// 映射檢視的極簡型
	private class ActiveBlacklistView
	{
		public int BlacklistID { get; set; }
		public int MemberID { get; set; }
		public string? Reason { get; set; }
		public byte SourceType { get; set; }
		public DateTime StartAt { get; set; }
		public DateTime? EndAt { get; set; }
		public int? LiftedByUserID { get; set; }
		public DateTime? LiftedAt { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	public class MemberListItem
	{
		public int MemberID { get; set; }
		public string Username { get; set; } = null!;
		public string? Email { get; set; }
		public string? Phone { get; set; }
		public byte Status { get; set; }
		public int TotalBooks { get; set; }
		public int TotalBorrows { get; set; }
		public bool IsBlacklistedNow { get; set; }
	}
}
