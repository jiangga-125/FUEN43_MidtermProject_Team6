using System.Threading.Tasks;

namespace BookLoop.Services.Security
{
	public interface ITokenService
	{
		Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(int memberId);

		// 簡易 TempToken：只塞 memberId + purpose，時效短
		string IssueTempToken(int memberId, int minutes, string purpose);

		// 從 Authorization: Bearer <tempToken> 嘗試解出 memberId（驗證失敗回 null）
		int? TryGetMemberIdFromTempToken(string authHeader);
	}
}
