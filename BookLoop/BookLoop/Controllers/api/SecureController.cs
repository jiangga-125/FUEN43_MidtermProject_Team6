using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Controllers.Api
{
    [ApiController]
    [Route("api/secure")]
    public class SecureController : ControllerBase
    {
        // 登入
        [HttpGet("hello")]
		[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)] // 同時接受Cookie 與 JWT
		public IActionResult Hello()
        {
            var name = User?.Identity?.Name ?? "(unknown)";
            return Ok(new { ok = true, message = $"Hello, {name}" });
        }
    }
}
