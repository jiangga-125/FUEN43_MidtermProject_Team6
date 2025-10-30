using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Controllers.Api
{
    [ApiController]
    [Route("api/secure")]
    public class SecureController : ControllerBase
    {
        // µn¤J
        [HttpGet("hello")]
        [Authorize] // ¨ü«OÅ@
        public IActionResult Hello()
        {
            var name = User?.Identity?.Name ?? "(unknown)";
            return Ok(new { ok = true, message = $"Hello, {name}" });
        }
    }
}
