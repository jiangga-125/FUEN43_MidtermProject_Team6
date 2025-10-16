using System.Security.Claims;
using BookLoop.Data;
using BookLoop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Controllers
{
	public class AuthController : Controller
	{
		private readonly AppDbContext _db;
		private readonly AuthService _auth;

		public AuthController(AppDbContext db, AuthService auth)
		{
			_db = db;
			_auth = auth;
		}

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Login(string? returnUrl = null)
		{
			ViewBag.ReturnUrl = returnUrl;
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
		{
			// 依你現有的 AuthService 規格驗證
			var user = await _auth.FindByEmailAsync(email);
			if (user == null || user.Status != 1)
				return Invalid("帳號不存在或未啟用", returnUrl);

			if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
				return Invalid("帳號暫時被鎖定", returnUrl);

			if (!_auth.VerifyPassword(user, password))
				return Invalid("密碼錯誤", returnUrl);

			// 登入成功：寫入最後登入與記錄（沿用你原本的作法）
			user.LastLoginAt = DateTime.UtcNow;
			user.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
			var ua = Request.Headers.UserAgent.ToString();
			await _auth.RecordLoginAsync(user.UserID, true, ip, ua);

			// 取權限鍵與供應商
			var rawPerms = await _auth.GetEffectivePermKeysAsync(user.UserID);
			var suppliers = await _auth.GetSupplierIdsAsync(user.UserID);

			// 可選：展開粗粒度到細粒度（保持與你原本慣例一致）
			var keys = new HashSet<string>(rawPerms, StringComparer.OrdinalIgnoreCase);
			if (keys.Contains("ADMIN"))
				keys.UnionWith(new[] { "Accounts.View", "Members.View", "Users.View" });
			if (keys.Contains("SALES"))
				keys.UnionWith(new[] { "Members.View" });
			if (keys.Contains("VENDOR"))
				keys.UnionWith(new[] { "Members.View" });

			// 建立 claims
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
				new Claim(ClaimTypes.Name, user.Email),
				new Claim("userType", user.UserType.ToString()),
				new Claim("mustChange", user.MustChangePassword ? "1" : "0")
			};
			foreach (var k in keys) claims.Add(new Claim("perm", k));
			foreach (var s in suppliers) claims.Add(new Claim("supplier", s.ToString()));

			var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
			await HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				new ClaimsPrincipal(id));

			// 需強制改密碼：導回 Account 區域的頁面（保留你現有流程）
			if (user.MustChangePassword)
				return RedirectToAction("ChangePassword", "Account");

			if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
				return Redirect(returnUrl);

			return RedirectToAction("Index", "Home");
		}

		[HttpPost]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return RedirectToAction(nameof(Login));
		}

		// 方便有時以 GET 呼叫 /Auth/Logout
		[HttpGet("Auth/Logout")]
		[AllowAnonymous]
		public IActionResult LogoutGet() => RedirectToAction(nameof(Login));

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Denied() => View("AccessDenied");

		private IActionResult Invalid(string message, string? returnUrl)
		{
			ModelState.AddModelError(string.Empty, message);
			ViewBag.ReturnUrl = returnUrl;
			return View("Login");
		}
	}
}
