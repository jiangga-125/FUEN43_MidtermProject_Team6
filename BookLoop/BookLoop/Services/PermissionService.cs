using BookLoop.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Services
{
	public class PermissionService
	{
		private readonly AppDbContext _db;
		public PermissionService(AppDbContext db) { _db = db; }

		/// <summary>
		/// 回傳此使用者擁有的所有 Features.Code（依 UserPermissions→Permission_Features→Features 展開）
		/// </summary>
		public async Task<HashSet<string>> ExpandFeaturesAsync(int userId, IEnumerable<string> permKeys)
		{
			// 以 UserPermissions 為根展開；permKeys 目前未用，預留你之後若要純集合鍵展開時使用
			var codes = await (
				from up in _db.UserPermissions
				join pf in _db.PermissionFeatures on up.PermissionID equals pf.PermissionID
				join f in _db.Features on pf.FeatureID equals f.FeatureID
				where up.UserID == userId
				select f.Code
			).Distinct().ToListAsync();

			return new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
		}
	}
}
