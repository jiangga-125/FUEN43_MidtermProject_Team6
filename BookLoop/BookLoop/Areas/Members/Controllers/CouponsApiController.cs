using BookLoop.Services;
using BookLoop.Services.Coupons;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Areas.Store.Controllers
{
	[Area("Store")]
	[Route("api/[area]/[controller]/[action]")]
	[ApiController]
	public class CouponsApiController : ControllerBase
	{
		private readonly CouponService _couponService;

		public CouponsApiController(CouponService couponService)
		{
			_couponService = couponService;
		}

		// GET: api/Store/CouponsApi/List
		[HttpGet]
		public IActionResult List(int memberId = 1)
		{
			var list = _couponService.GetAvailableCoupons(memberId);
			return Ok(list);
		}

		// POST: api/Store/CouponsApi/Apply
		[HttpPost]
		public async Task<IActionResult> Apply([FromBody] ApplyCouponRequest req)
		{
			var result = await _couponService.PreviewDiscountAsync(req.Code, req.MemberID, req.Subtotal);

			if (!result.IsSuccess)
				return BadRequest(new { message = result.ErrorMessage });

			return Ok(new
			{
				discount = result.Value.Discount,
				rule = result.Value.RuleText
			});
		}

		public class ApplyCouponRequest
		{
			public string Code { get; set; } = null!;
			public int MemberID { get; set; } = 1;
			public decimal Subtotal { get; set; }
		}
	}
}
