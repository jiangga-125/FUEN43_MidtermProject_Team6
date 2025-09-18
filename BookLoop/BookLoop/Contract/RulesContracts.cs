namespace BookLoop.Contracts;

// 請求 DTO
public record PreviewRequest(int MemberId, decimal Subtotal, string? CouponCode, int UsePoints);
public record ApplyRequest(int MemberId, string ExternalOrderNo, decimal Subtotal, string? CouponCode, int UsePoints, string? IdempotencyKey);

// 統一錯誤回應
public sealed class ErrorResponse
{
	public bool Ok { get; init; } = false;
	public string Code { get; init; } = "SERVER_ERROR";
	public string Message { get; init; } = "發生未預期錯誤";
	public string? TraceId { get; init; }
}

// 成功回應（/rules/apply）
public sealed class ApplySuccessResponse
{
	public bool Ok { get; init; } = true;
	public string Message { get; init; } = "套用完成";
	public object Data { get; init; } = default!;
}

// 規則試算結果（沿用你現有 PricingResult 結構；如無則定義）
public sealed class PricingResultDto
{
	public decimal Subtotal { get; init; }
	public decimal CouponDiscount { get; init; }
	public decimal AfterCoupon { get; init; }
	public int PointsUsed { get; init; }
	public decimal Payable { get; init; }
	public string? CouponMessage { get; init; }
	public string? PointsMessage { get; init; }
}

public record RefundRequest(string ExternalOrderNo, string Reason);
