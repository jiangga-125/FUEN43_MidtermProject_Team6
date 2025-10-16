using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace BookLoop.Authorization
{
	/// <summary>
	/// 檢查目前使用者是否擁有指定的權限鍵（perm claim）
	/// </summary>
	public class PermissionAuthorizationHandler
		: AuthorizationHandler<PermissionRequirement>
	{
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext context,
			PermissionRequirement requirement)
		{
			if (context.User?.Identity?.IsAuthenticated != true)
				return Task.CompletedTask;

			// 支援 requirement.PermissionKey 與別名 requirement.Key
			var key = requirement.PermissionKey;

			var has = context.User.Claims.Any(c =>
				c.Type == "perm" &&
				string.Equals(c.Value, key, System.StringComparison.OrdinalIgnoreCase));

			if (has)
				context.Succeed(requirement);

			return Task.CompletedTask;
		}
	}
}
