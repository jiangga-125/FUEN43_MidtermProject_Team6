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
			// 1) 基本檢查
			var user = await _auth.FindByEmailAsync(email);
			if (user == null || user.Status != 1)
				return Invalid("帳號不存在或未啟用", returnUrl);

			if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
				return Invalid("帳號暫時被鎖定", returnUrl);

			if (!_auth.VerifyPassword(user, password))
				return Invalid("密碼錯誤", returnUrl);

			// 2) 更新最後登入
			user.LastLoginAt = DateTime.UtcNow;
			user.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			await _auth.RecordLoginAsync(user.UserID, true,
				HttpContext.Connection.RemoteIpAddress?.ToString(),
				Request.Headers.UserAgent.ToString());

			// 3) 重要：讓 AuthService 依「Permission_Features → Features」查出所有 Feature Codes，
			//    並發出對應的 perm claims（例如 Account.Access / Users.Index / ...）
			await _auth.SignInAsync(user, isPersistent: true);

			// 4) 流程收尾
			if (user.MustChangePassword)
				return RedirectToAction("ChangePassword", "Account"); // 保持你原有流程

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
