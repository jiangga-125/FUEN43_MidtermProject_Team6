using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BookLoop.Data;
using BookLoop;

namespace Account.Controllers
{
	[Area("Account")]
	[Authorize]
	public class AccountController : Controller
	{
		private readonly AppDbContext _db;
		private readonly PasswordHasher<User> _hasher = new();

		public AccountController(AppDbContext db) => _db = db;

		[HttpGet]
		public async Task<IActionResult> ChangePassword()
		{
			// 顯示使用者 Email 方便辨識
			var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
			var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == uid);
			if (u == null) return NotFound();
			ViewBag.Email = u.Email;
			return View();
		}

		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword(string currentPwd, string newPwd, string confirmPwd)
		{
			if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 8)
			{
				TempData["err"] = "新密碼至少 8 碼。";
				return RedirectToAction(nameof(ChangePassword));
			}
			if (newPwd != confirmPwd)
			{
				TempData["err"] = "兩次輸入的新密碼不一致。";
				return RedirectToAction(nameof(ChangePassword));
			}

			var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
			var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == uid);
			if (u == null) return NotFound();

			// 驗證舊密碼（若使用者被強制改密碼，仍建議要求輸入舊密碼）
			if (!string.IsNullOrWhiteSpace(u.PasswordHash))
			{
				var verify = _hasher.VerifyHashedPassword(u, u.PasswordHash, currentPwd);
				if (verify == PasswordVerificationResult.Failed)
				{
					TempData["err"] = "目前密碼不正確。";
					return RedirectToAction(nameof(ChangePassword));
				}
			}

			// 更新密碼 & 清除 MustChangePassword
			u.PasswordHash = _hasher.HashPassword(u, newPwd);
			u.MustChangePassword = false;
			u.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			// 重新登入：更新 mustChange claim -> 0
			var existingClaims = User.Claims.Where(c => c.Type != "mustChange").ToList();
			existingClaims.Add(new Claim("mustChange", "0"));
			var identity = new ClaimsIdentity(existingClaims, CookieAuthenticationDefaults.AuthenticationScheme);
			await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

			TempData["ok"] = "密碼已更新。";
			return RedirectToAction("Index", "Home");
		}
	}
}
