using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using BookLoop.Contracts;
using BookLoop.Models;
using BookLoop.Services.Points;
using BookLoop.Services.Pricing;
using BookLoop.Data;

namespace BookLoop.Controllers;

[ApiController]
[Route("rules")]
public class RulesController : ControllerBase
{
	private readonly MemberContext _db;
	private readonly IPricingEngine _pricing;
	private readonly IPointsService _points;

	public RulesController(MemberContext db, IPricingEngine pricing, IPointsService points)
	{
		_db = db; _pricing = pricing; _points = points;
	}

	/// <summary>試算優惠券 + 點數，不落帳</summary>
	[HttpPost("preview")]
	[ProducesResponseType(typeof(PricingResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
	public async Task<IActionResult> Preview([FromBody] PreviewRequest req)
	{
		if (!await _db.Members.AnyAsync(m => m.MemberID == req.MemberId))
			return BadRequest(new ErrorResponse { Code = "MEMBER_NOT_FOUND", Message = $"會員 {req.MemberId} 不存在", TraceId = Activity.Current?.Id });

		var result = await _pricing.PreviewAsync(new PricingInputs
		{
			MemberId = req.MemberId,
			Subtotal = req.Subtotal,
			CouponCode = req.CouponCode,
			UsePoints = req.UsePoints
		});

		// 這裡假設 Preview 不丟規則錯；若你在引擎內回錯誤，請改 422
		var dto = new PricingResultDto
		{
			Subtotal = result.Subtotal,
			CouponDiscount = result.CouponDiscount,
			AfterCoupon = result.AfterCoupon,
			PointsUsed = result.PointsUsed,
			Payable = result.Payable,
			CouponMessage = result.CouponMessage,
			PointsMessage = result.PointsMessage
		};
		return Ok(dto);
	}

	/// <summary>套用規則（扣點 + 寫快照），不建立你的 Orders</summary>
	[HttpPost("apply")]
	[ProducesResponseType(typeof(ApplySuccessResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> Apply([FromBody] ApplyRequest req)
	{
		if (!await _db.Members.AnyAsync(m => m.MemberID == req.MemberId))
			return BadRequest(new ErrorResponse { Code = "MEMBER_NOT_FOUND", Message = $"會員 {req.MemberId} 不存在", TraceId = Activity.Current?.Id });

		// 冪等：同一 ExternalOrderNo 不得重複
		if (await _db.RuleApplications.AnyAsync(x => x.ExternalOrderNo == req.ExternalOrderNo))
			return StatusCode(StatusCodes.Status409Conflict,
				new ErrorResponse { Code = "DUPLICATE_EXTERNAL_ORDER", Message = "此外部訂單已套用過規則", TraceId = Activity.Current?.Id });

		// 試算（與 Preview 一致）
		var preview = await _pricing.PreviewAsync(new PricingInputs
		{
			MemberId = req.MemberId,
			Subtotal = req.Subtotal,
			CouponCode = req.CouponCode,
			UsePoints = req.UsePoints
		});

		using var tx = await _db.Database.BeginTransactionAsync();
		try
		{
			// 扣點（有用才扣）
			if (preview.PointsUsed > 0)
			{
				var ded = await _points.DeductAsync(req.MemberId, preview.PointsUsed, null, "USE_FOR_EXTERNAL_ORDER", req.ExternalOrderNo);
				if (!ded.Ok)
					return StatusCode(StatusCodes.Status422UnprocessableEntity,
						new ErrorResponse { Code = "BUSINESS_RULE_VIOLATION", Message = ded.Message ?? "點數扣抵失敗", TraceId = Activity.Current?.Id });
			}

			// 券快照（不綁內部 Orders）
			byte? typeSnap = null; decimal? valSnap = null;
			if (!string.IsNullOrWhiteSpace(req.CouponCode))
			{
				var c = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(x => x.Code == req.CouponCode);
				if (c != null) { typeSnap = (byte)c.DiscountType; valSnap = c.DiscountValue; }
			}

			_db.RuleApplications.Add(new RuleApplication
			{
				ExternalOrderNo = req.ExternalOrderNo,
				MemberID = req.MemberId,
				CouponCodeSnap = req.CouponCode,
				CouponTypeSnap = typeSnap,
				CouponValueSnap = valSnap,
				CouponDiscountAmount = preview.CouponDiscount,
				PointsUsed = preview.PointsUsed,
				Subtotal = preview.Subtotal,
				Payable = preview.Payable
			});

			await _db.SaveChangesAsync();
			await tx.CommitAsync();

			return Ok(new ApplySuccessResponse
			{
				Message = "套用完成",
				Data = new { pointsUsed = preview.PointsUsed, couponDiscount = preview.CouponDiscount }
			});
		}
		catch (DbUpdateException ex)
		{
			await tx.RollbackAsync();
			return StatusCode(StatusCodes.Status500InternalServerError,
				new ErrorResponse { Code = "DB_ERROR", Message = ex.GetBaseException().Message, TraceId = Activity.Current?.Id });
		}
		catch (Exception ex)
		{
			await tx.RollbackAsync();
			return StatusCode(StatusCodes.Status500InternalServerError,
				new ErrorResponse { Code = "SERVER_ERROR", Message = ex.Message, TraceId = Activity.Current?.Id });
		}
	}

	/// <summary>取消訂單 → 回補點數</summary>
	[HttpPost("refund")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> Refund([FromBody] RefundRequest req)
	{
		var ra = await _db.RuleApplications.FirstOrDefaultAsync(x => x.ExternalOrderNo == req.ExternalOrderNo);
		if (ra == null)
			return NotFound(new ErrorResponse { Code = "NOT_FOUND", Message = "查無此訂單套用紀錄" });

		if (ra.Status == "REFUNDED")
			return StatusCode(409, new ErrorResponse { Code = "ALREADY_REFUNDED", Message = "此訂單已退款" });

		using var tx = await _db.Database.BeginTransactionAsync();
		try
		{
			if (ra.PointsUsed > 0)
			{
				// 回補點數：注意這裡是正數，等於發放回去
				var result = await _points.DeductAsync(ra.MemberID, -ra.PointsUsed, null, "REFUND_EXTERNAL_ORDER", ra.ExternalOrderNo);
				if (!result.Ok)
				{
					return StatusCode(500, new ErrorResponse { Code = "POINTS_REFUND_FAILED", Message = result.Message ?? "退點失敗" });
				}
			}

			// 更新 RuleApplications 狀態
			ra.Status = "REFUNDED";
			ra.RefundedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			await tx.CommitAsync();
			return Ok(new { ok = true, message = "已退款完成", data = new { pointsRefunded = ra.PointsUsed, externalOrderNo = ra.ExternalOrderNo } });
		}
		catch (Exception ex)
		{
			await tx.RollbackAsync();
			return StatusCode(500, new ErrorResponse { Code = "SERVER_ERROR", Message = ex.Message });
		}
	}

}
