// /Services/Points/PointsService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using BookLoop.Models;
using BookLoop.Services.Common;
using BookLoop.Data;

namespace BookLoop.Services.Points
{
	public class PointsService : IPointsService
	{
		private readonly MemberContext _db;

		public PointsService(MemberContext db) => _db = db;

		public async Task<int> GetBalanceAsync(int memberId)
		{
			var mp = await _db.MemberPoints.AsNoTracking().FirstOrDefaultAsync(x => x.MemberPointID == memberId);
			return mp?.Balance ?? 0; // 找不到代表 0
		}

		// 20% 上限（券後金額 * 0.2，四捨五入到整數點）
		public int CalcMaxUsablePoints(decimal amountAfterCoupon)
		{
			// 點數=元，故直接取金額 * 0.2
			var max = Math.Floor(amountAfterCoupon * 0.2m);
			return (int)max;
		}

		public async Task<Result<bool>> DeductAsync(int memberId, int points, int? orderId, string reasonCode, string? externalOrderNo = null)
		{
			if (points < 0) points = Math.Abs(points); // 自動轉成正數再扣

			var mp = await _db.MemberPoints.FirstOrDefaultAsync(x => x.MemberPointID == memberId);
			if (mp == null) return Result<bool>.Fail("找不到會員點數帳戶");
			if (mp.Balance < points) return Result<bool>.Fail("點數不足");

			mp.Balance -= points;             // 扣掉餘額
			mp.UpdatedAt = DateTime.Now;      // 更新時間（本地時間即可）

			_db.PointsLedgers.Add(new PointsLedger
			{
				MemberID = memberId,
				Delta = -points,              // 負數=扣點
				ReasonCode = reasonCode,      // 例："USE_FOR_ORDER"
				OrderId = orderId,
				ExternalOrderNo = externalOrderNo, // ★ 新欄位：外部訂單號
				CreatedAt = DateTimeOffset.Now
			});

			await _db.SaveChangesAsync();
			return Result<bool>.Success(true);
		}

		public async Task<Result<bool>> CreditAsync(int memberId, int points, int? orderId, string reasonCode)
		{
			if (points <= 0) return Result<bool>.Success(true);

			var mp = await _db.MemberPoints.FirstOrDefaultAsync(x => x.MemberPointID == memberId);
			if (mp == null)
			{
				mp = new MemberPoint
				{
					MemberPointID = memberId,
					Balance = 0,
					UpdatedAt = DateTime.Now
				};
				_db.MemberPoints.Add(mp);
			}

			mp.Balance += points;             // 加點
			mp.UpdatedAt = DateTime.Now;


			_db.PointsLedgers.Add(new PointsLedger
			{
				MemberID = memberId,
				Delta = -points,               // 正數=加點
				ReasonCode = reasonCode,      // 例："EARN_BY_ORDER"
				OrderId = orderId,
				CreatedAt = DateTimeOffset.Now
			});

			await _db.SaveChangesAsync();
			return Result<bool>.Success(true);
		}
	}
}
