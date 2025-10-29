using System.Linq;
using System.Security.Claims;

namespace BookLoop.Security
{
	public static class ClaimsPrincipalExtensions
	{
		public static bool HasPerm(this ClaimsPrincipal? user, string key)
			=> user?.Identity?.IsAuthenticated == true
			   && user.Claims.Any(c => c.Type == "perm" && c.Value == key);

		public static bool HasAnyPerm(this ClaimsPrincipal? user, params string[] keys)
			=> user?.Identity?.IsAuthenticated == true
			   && user.Claims.Any(c => c.Type == "perm" && keys.Contains(c.Value));
	}
}
