using System.Threading.Tasks;

namespace BookLoop.Services.Security
{
	public interface IOtpService
	{
		Task<(bool ok, string maskedEmail, int cooldown)> IssueAsync(
			int memberId, string tokenType, string? deviceHash = null);

		Task<(bool ok, string? error)> VerifyAsync(
			int memberId, string tokenType, string code, string? deviceHash = null, bool rememberDevice = false);
	}
}
