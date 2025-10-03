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
		if (user == null || user.Status != 1) return Invalid("帳號不存在或未啟用");

		if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
			return Invalid("帳號暫時被鎖定");

		var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
		var ua = Request.Headers.UserAgent.ToString();

		// 密碼驗證如需啟用，還原你原本的 VerifyPassword 區塊

		// 成功登入後記錄
		user.LastLoginAt = DateTime.UtcNow;
		user.UpdatedAt = DateTime.UtcNow;
		await _db.SaveChangesAsync();
		await _auth.RecordLoginAsync(user.UserID, true, ip, ua);

		// 1) 先撈出目前 DB 回來的權限鍵（現在可能只有 ADMIN/SALES/VENDOR）
		var rawPerms = await _auth.GetEffectivePermKeysAsync(user.UserID);
		var suppliers = await _auth.GetSupplierIdsAsync(user.UserID);

		// 2) 把粗粒度角色展開成你程式需要的細粒度鍵
		var keys = new HashSet<string>(rawPerms ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

		if (keys.Contains("ADMIN"))
		{
			keys.UnionWith(new[]{
				"Accounts.View","Accounts.Edit",
				"Permissions.Manage",
				"Blacklists.View","Blacklists.Manage",
				"Members.View","Members.Edit"
			});
		}
		if (keys.Contains("SALES"))
		{
			// 依需求擴充；先給最小可見頁面
			keys.UnionWith(new[] { "Members.View" });
		}
		if (keys.Contains("VENDOR"))
		{
			// 依需求擴充
			keys.UnionWith(new[] { "Members.View" });
		}

		// 3) 建立 claims（加入展開後的細粒度鍵）
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

		// ★若需要強制改密碼，這裡導回 Account 區域的頁面（依你的實際 action 調整）
		if (user.MustChangePassword)
			return RedirectToAction("ChangePassword", "Auth", new { area = "Account" });

		if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
			return Redirect(returnUrl);

		// 回到 Account 區域首頁
		return RedirectToAction("Index", "Home", new { area = "Account" });

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
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToAction(nameof(Login));
	}

	public IActionResult Denied() => Content("Access Denied");
}
