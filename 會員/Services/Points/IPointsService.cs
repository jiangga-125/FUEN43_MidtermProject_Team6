// /Services/Points/IPointsService.cs
using System.Threading.Tasks;
using 會員.Services.Common;

namespace 會員.Services.Points
{
	public interface IPointsService
	{
		// 查詢會員目前可用點數
		Task<int> GetBalanceAsync(int memberId);

		// 試算可用點數（受 20% 上限限制）
		int CalcMaxUsablePoints(decimal amountAfterCoupon);

		// 扣點（寫入 Ledger + 更新餘額）
		Task<Result<bool>> DeductAsync(int memberId, int points, int? orderId, string reason, string? externalOrderNo = null);

		// 發點（下單完成後，若有規則）
		Task<Result<bool>> CreditAsync(int memberId, int points, int? orderId, string reasonCode);
	}
}
