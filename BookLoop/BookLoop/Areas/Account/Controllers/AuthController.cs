using BookLoop.Data;
using BookLoop;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BookLoop.Services;

namespace Account.Controllers;

[Area("Account")]
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
	public IActionResult Login(string? returnUrl = null)
	{
		ViewBag.ReturnUrl = returnUrl;
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
	{
		var user = await _auth.FindByEmailAsync(email);
		if (user == null || user.Status != 1)
		{
			return Invalid("帳號不存在或未啟用");
		}

		if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
		{
			return Invalid("帳號暫時被鎖定");
		}

		var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
		var ua = Request.Headers.UserAgent.ToString();

		//if (!_auth.VerifyPassword(user, password))
		//{
		//	user.FailedAccessCount += 1;
		//	if (user.FailedAccessCount >= 5)
		//	{
		//		user.LockoutEndAt = DateTime.UtcNow.AddMinutes(15);
		//		user.FailedAccessCount = 0; // 達門檻後歸零
		//	}
		//	user.UpdatedAt = DateTime.UtcNow;
		//	await _db.SaveChangesAsync();
		//	await _auth.RecordLoginAsync(user.UserID, false, ip, ua, "密碼錯誤");
		//	return Invalid("帳密錯誤");
		//}

		// 檢查黑名單（可視需要限制登入或限制操作）
		//var isBlacklisted = await new Services.PermissionService(_db).IsBlacklistedAsync(user.UserID);
		// 這裡選擇允許登入，但後續操作會受限（若你要禁止登入，可以直接 return Invalid("使用者在黑名單中")）

		// 成功登入
		//user.FailedAccessCount = 0;
		user.LastLoginAt = DateTime.UtcNow;
		user.UpdatedAt = DateTime.UtcNow;
		await _db.SaveChangesAsync();
		await _auth.RecordLoginAsync(user.UserID, true, ip, ua);

		// 建立 Claims
		var perms = await _auth.GetEffectivePermKeysAsync(user.UserID);
		var suppliers = await _auth.GetSupplierIdsAsync(user.UserID);

		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
			new Claim(ClaimTypes.Name, user.Email),
			new Claim("userType", user.UserType.ToString()),
			new Claim("mustChange", user.MustChangePassword ? "1" : "0") // ★新增：是否需改密碼
		};
		foreach (var k in perms) claims.Add(new Claim("perm", k));
		foreach (var s in suppliers) claims.Add(new Claim("supplier", s.ToString()));

		var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));

		// ★若需要強制改密碼，直接導去 ChangePassword
		if (user.MustChangePassword)
			return RedirectToAction("ChangePassword", "Account");

		if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
			return Redirect(returnUrl);
		return RedirectToAction("Index", "Home");

		IActionResult Invalid(string m)
		{
			ModelState.AddModelError(string.Empty, m);
			ViewBag.ReturnUrl = returnUrl;
			return View();
		}
	}

	[HttpPost]
	public async Task<IActionResult> Logout()
	{
		await HttpContext.SignOutAsync();
		return RedirectToAction(nameof(Login));
	}

	public IActionResult Denied() => Content("Access Denied");
}
