using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookLoop.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Services
{
	/// <summary>
	/// 直接以 USER_PERMISSIONS 判斷使用者權限（不使用 PermissionSet）
	/// </summary>
	public class PermissionService
	{
		private readonly AppDbContext _db;
		public PermissionService(AppDbContext db) => _db = db;

		public async Task<bool> HasAsync(int userId, string permKey)
		{
			if (string.IsNullOrWhiteSpace(permKey)) return false;

			var q =
				from up in _db.UserPermissions
				where up.UserID == userId
				join p in _db.Permissions on up.PermissionID equals p.PermissionID
				where p.PermKey == permKey
				select 1;

			return await q.AnyAsync();
		}

		public async Task<List<string>> GetEffectivePermKeysAsync(int userId)
		{
			var q =
				from up in _db.UserPermissions
				where up.UserID == userId
				join p in _db.Permissions on up.PermissionID equals p.PermissionID
				select p.PermKey;

			return await q.Distinct().OrderBy(x => x).ToListAsync();
		}

		public async Task<Dictionary<int, List<string>>> GetUsersEffectivePermsAsync(IEnumerable<int> userIds)
		{
			var ids = userIds?.Distinct().ToArray() ?? System.Array.Empty<int>();
			if (ids.Length == 0) return new();

			var q =
				from up in _db.UserPermissions
				where ids.Contains(up.UserID)
				join p in _db.Permissions on up.PermissionID equals p.PermissionID
				select new { up.UserID, p.PermKey };

			var rows = await q.ToListAsync();
			return rows
				.GroupBy(x => x.UserID)
				.ToDictionary(
					g => g.Key,
					g => g.Select(x => x.PermKey).Distinct().OrderBy(x => x).ToList()
				);
		}

		// 後台 User 不用黑名單
		public Task<bool> IsBlacklistedAsync(int userId) => Task.FromResult(false);
		// 前台會員黑名單（如需）
		public Task<bool> IsMemberBlacklistedAsync(int memberId, System.DateTime? nowUtc = null)
			=> Task.FromResult(false); // 這裡若要用 Blacklists 檢查，可改成 EF AnyAsync
	}
}
