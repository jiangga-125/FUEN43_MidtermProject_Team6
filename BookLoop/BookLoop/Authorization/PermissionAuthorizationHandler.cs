using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using BookLoop.Services;

namespace BookLoop.Authorization
{
	public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
	{
		private readonly PermissionService _permService;
		private readonly IMemoryCache _cache;

		public PermissionAuthorizationHandler(PermissionService permService, IMemoryCache cache)
		{
			_permService = permService;
			_cache = cache;
		}

		protected override async Task HandleRequirementAsync(
			AuthorizationHandlerContext context,
			PermissionRequirement requirement)
		{
			if (context.User?.Identity?.IsAuthenticated != true) return;

			var feature = requirement.PermissionKey;

			// 相容舊邏輯：cookie 已含 feature 直接放行
			if (context.User.HasClaim("perm", feature))
			{
				context.Succeed(requirement);
				return;
			}

			// 取使用者 ID
			var uidStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!int.TryParse(uidStr, out var userId)) return;

			// 從 cookie 取集合鍵 & 版本戳
			var permKeys = context.User.FindAll("permkey").Select(c => c.Value).ToArray();
			var version = context.User.FindFirst("permver")?.Value ?? "v1";

			// 快取 Key：依使用者與版本戳
			var cacheKey = $"perm:u:{userId}:v:{version}:features";

			// 展開後的 features（快取 10 分鐘）
			var features = await _cache.GetOrCreateAsync(cacheKey, async entry =>
			{
				entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
				return await _permService.ExpandFeaturesAsync(userId, permKeys);
			});

			if (features.Contains(feature, StringComparer.OrdinalIgnoreCase))
				context.Succeed(requirement);
		}
	}
}
