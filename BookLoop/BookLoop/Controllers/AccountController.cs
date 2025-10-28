using System.Security.Claims;
using BookLoop.Data;
using BookLoop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Controllers
{
	[Authorize]
	public class AccountController : Controller
	{
		private readonly AppDbContext _db;
		private readonly AuthService _auth;

		public AccountController(AppDbContext db, AuthService auth)
		{
			_db = db;
			_auth = auth;
		}

		// ===== 只保留「修改密碼」 =====

		[HttpGet]
		public IActionResult ChangePassword() => View();

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword(
			string oldPassword,
			string newPassword,
			string confirmPassword)
		{
			if (string.IsNullOrWhiteSpace(oldPassword) ||
				string.IsNullOrWhiteSpace(newPassword) ||
				string.IsNullOrWhiteSpace(confirmPassword))
			{
				ModelState.AddModelError(string.Empty, "請完整填寫欄位");
				return View();
			}

			if (newPassword != confirmPassword)
			{
				ModelState.AddModelError(string.Empty, "兩次輸入的新密碼不一致");
				return View();
			}

			var email = User.Identity?.Name;
			if (string.IsNullOrWhiteSpace(email))
			{
				ModelState.AddModelError(string.Empty, "找不到使用者身分");
				return View();
			}

			var user = await _auth.FindByEmailAsync(email);
			if (user == null)
			{
				ModelState.AddModelError(string.Empty, "使用者不存在");
				return View();
			}

			// 驗證舊密碼（沿用你的 AuthService）
			if (!_auth.VerifyPassword(user, oldPassword))
			{
				ModelState.AddModelError(string.Empty, "目前密碼不正確");
				return View();
			}

			// 產生新密碼雜湊：只使用你 AuthService 的雜湊方法；抓不到就拒絕變更（避免寫壞）
			string? newHash = null;
			var t = _auth.GetType();

			var m1 = t.GetMethod("HashPassword", new[] { typeof(string) });
			if (m1 != null) newHash = m1.Invoke(_auth, new object[] { newPassword }) as string;

			if (newHash == null)
			{
				var m2 = t.GetMethod("GeneratePasswordHash", new[] { typeof(string) })
					  ?? t.GetMethod("CreatePasswordHash", new[] { typeof(string) })
					  ?? t.GetMethod("ComputePasswordHash", new[] { typeof(string) });
				if (m2 != null) newHash = m2.Invoke(_auth, new object[] { newPassword }) as string;
			}

			if (string.IsNullOrEmpty(newHash))
			{
				ModelState.AddModelError(string.Empty,
					"系統沒有找到設定新密碼的方式，已取消修改（避免造成無法登入）。請確認 AuthService 提供 HashPassword 類方法。");
				return View();
			}

			user.PasswordHash = newHash!;
			user.MustChangePassword = false;
			user.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();

			TempData["msg"] = "密碼已更新";
			return View();
		}
	}
}
