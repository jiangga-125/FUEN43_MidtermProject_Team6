using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BookLoop.Authorization
{
	/// <summary>
	/// 依照 [Authorize(Policy="xxx")] 動態產生 Policy
	/// </summary>
	public class PermissionPolicyProvider : IAuthorizationPolicyProvider
	{
		private readonly DefaultAuthorizationPolicyProvider _fallback;
		private readonly AuthorizationOptions _options;

		public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
		{
			_options = options.Value;
			_fallback = new DefaultAuthorizationPolicyProvider(options);
		}

		public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
			=> _fallback.GetDefaultPolicyAsync();

		public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
			=> _fallback.GetFallbackPolicyAsync();

		public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
		{
			// 先看是否在 options 裡有靜態註冊
			var existing = _options.GetPolicy(policyName);
			if (existing != null) return Task.FromResult<AuthorizationPolicy?>(existing);

			// 動態建構：policyName 就是權限鍵
			var policy = new AuthorizationPolicyBuilder()
				.AddRequirements(new PermissionRequirement(policyName))
				.Build();

			return Task.FromResult<AuthorizationPolicy?>(policy);
		}
	}
}
