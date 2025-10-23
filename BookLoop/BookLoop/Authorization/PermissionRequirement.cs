using Microsoft.AspNetCore.Authorization;

namespace BookLoop.Authorization
{
	/// <summary>
	/// 權限需求：需要具備某個權限鍵（例如 "Users.View"）
	/// </summary>
	public class PermissionRequirement : IAuthorizationRequirement
	{
		/// <summary>
		/// 權限鍵（給 Handler 使用）
		/// </summary>
		public string PermissionKey { get; }

		// 為了相容可能舊代碼，提供別名（避免別處用 requirement.Key）
		public string Key => PermissionKey;

		public PermissionRequirement(string permissionKey)
		{
			PermissionKey = permissionKey;
		}
	}
}
