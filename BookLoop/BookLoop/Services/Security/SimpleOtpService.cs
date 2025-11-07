// BookLoop.Services.Security.SimpleOtpService.cs
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BookLoop;
using MemberTokenEntity = global::BookLoop.Models.MemberToken;

namespace BookLoop.Services.Security
{
	public class SimpleOtpService : IOtpService
	{
		private readonly AppDbContext _appDb;
		private readonly MemberContext _memberDb;
		private readonly IMailService _mail;
		private readonly EmailOtpOptions _opt;

		public SimpleOtpService(
			AppDbContext appDb,
			MemberContext memberDb,
			IMailService mail,
			IOptions<EmailOtpOptions> opt)
		{
			_appDb = appDb;
			_memberDb = memberDb;
			_mail = mail;
			_opt = opt.Value;
		}

		public async Task<(bool ok, string maskedEmail, int cooldown)> IssueAsync(
			int memberId, string tokenType, string? deviceHash = null)
		{
			var m = await _memberDb.Members.AsNoTracking()
				.FirstAsync(x => x.MemberID == memberId);

			// 冷卻
			var recent = await _appDb.MemberTokens
				.Where(t => t.MemberID == memberId && t.TokenType == tokenType)
				.OrderByDescending(t => t.CreatedAt)
				.FirstOrDefaultAsync();

			if (recent != null)
			{
				var seconds = (int)(DateTime.UtcNow - recent.CreatedAt).TotalSeconds;
				if (seconds < _opt.ResendCooldownSeconds)
					return (false, MaskEmail(m.Email), _opt.ResendCooldownSeconds - seconds);
			}

			var code = GenerateNumericCode(_opt.CodeLength);

			// ★ 這裡用的是 BookLoop.Models.MemberToken（Token 為 string）
			var token = new MemberTokenEntity
			{
				MemberID = memberId,
				TokenType = tokenType,
				Token = code,
				ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_opt.ExpireMinutes),
				CreatedAt = DateTime.UtcNow
			};
			_appDb.MemberTokens.Add(token);
			await _appDb.SaveChangesAsync();

			var subject = tokenType switch
			{
				"Login.EmailOTP" => "你的 BookLoop 登入驗證碼",
				"Register.EmailCode" => "你的 BookLoop 註冊驗證碼",
				"PasswordReset.EmailCode" => "你的 BookLoop 重設密碼驗證碼",
				_ => "你的 BookLoop 驗證碼"
			};

			var html = $@"<p>你的驗證碼：<strong style=""font-size:20px;"">{code}</strong></p>
                          <p>{_opt.ExpireMinutes} 分鐘內有效，請勿轉給他人。</p>";
			await _mail.SendAsync(m.Email, subject, html);

			return (true, MaskEmail(m.Email), _opt.ResendCooldownSeconds);
		}

		public async Task<(bool ok, string? error)> VerifyAsync(
			int memberId, string tokenType, string code, string? deviceHash = null, bool rememberDevice = false)
		{
			var t = await _appDb.MemberTokens
				.Where(x => x.MemberID == memberId
						 && x.TokenType == tokenType
						 && x.ConsumedAtUtc == null
						 && x.ExpiresAtUtc > DateTime.UtcNow)
				.OrderByDescending(x => x.CreatedAt)
				.FirstOrDefaultAsync();

			if (t == null) return (false, "expired");

			// Token 現在是 string，比對不會再出現 byte/string 錯誤
			if (!string.Equals(t.Token, code, StringComparison.Ordinal))
				return (false, "invalid");

			t.ConsumedAtUtc = DateTime.UtcNow;
			await _appDb.SaveChangesAsync();

			if (rememberDevice && tokenType == "Login.EmailOTP" && !string.IsNullOrWhiteSpace(deviceHash))
			{
				var d = await _appDb.MemberTrustedDevices
					.FirstOrDefaultAsync(x => x.MemberID == memberId && x.DeviceHash == deviceHash);
				if (d == null)
				{
					_appDb.MemberTrustedDevices.Add(new MemberTrustedDevice
					{
						MemberID = memberId,
						DeviceHash = deviceHash,
						DeviceName = "我的裝置",
						CreatedAt = DateTime.UtcNow,
						LastUsedAt = DateTime.UtcNow,
						ExpiresAtUtc = DateTime.UtcNow.AddDays(_opt.BypassIfTrustedDeviceDays)
					});
				}
				else
				{
					d.LastUsedAt = DateTime.UtcNow;
					d.ExpiresAtUtc = DateTime.UtcNow.AddDays(_opt.BypassIfTrustedDeviceDays);
				}
				await _appDb.SaveChangesAsync();
			}

			return (true, null);
		}

		private static string GenerateNumericCode(int len)
		{
			var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(len);
			var sb = new StringBuilder(len);
			foreach (var b in bytes) sb.Append((b % 10).ToString());
			return sb.ToString();
		}

		private static string MaskEmail(string email)
		{
			var at = email.IndexOf('@');
			if (at <= 1) return "***";
			var name = email[..at];
			var domain = email[(at + 1)..];
			static string mask(string s) =>
				s.Length <= 1 ? "*" : s[0] + new string('*', Math.Max(1, s.Length - 2)) + s[^1];
			return $"{mask(name)}@{mask(domain)}";
		}
	}
}
