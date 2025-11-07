using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BookLoop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BookLoop.Services.Security
{
	public class JwtTokenService : ITokenService
	{
		private readonly IConfiguration _cfg;
		private readonly MemberContext _memberDb;
		private readonly SymmetricSecurityKey _key;
		private readonly string _issuer, _aud;
		private readonly int _accessMinutes, _refreshDays;

		public JwtTokenService(IConfiguration cfg, MemberContext memberDb)
		{
			_cfg = cfg;
			_memberDb = memberDb;

			var rawKey = _cfg["Jwt:Key"]!;
			try { _key = new SymmetricSecurityKey(Convert.FromBase64String(rawKey)); }
			catch { _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey)); }

			_issuer = _cfg["Jwt:Issuer"]!;
			_aud = _cfg["Jwt:Audience"]!;
			_accessMinutes = int.Parse(_cfg["Jwt:AccessTokenMinutes"] ?? "15");
			_refreshDays = int.Parse(_cfg["Jwt:RefreshTokenDays"] ?? "30");
		}

		public async Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(int memberId)
		{
			var m = await _memberDb.Members.AsNoTracking()
				.FirstAsync(x => x.MemberID == memberId);

			var now = DateTime.UtcNow;
			var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
			var claims = new[]
			{
				new Claim("mid", m.MemberID.ToString()),
				new Claim(ClaimTypes.Email, m.Email ?? string.Empty),
				new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(m.Email) ? $"Member#{m.MemberID}" : m.Email)
			};

			var access = new JwtSecurityToken(
				issuer: _issuer,
				audience: _aud,
				claims: claims,
				notBefore: now,
				expires: now.AddMinutes(_accessMinutes),
				signingCredentials: creds
			);
			var accessToken = new JwtSecurityTokenHandler().WriteToken(access);

			var refresh = new JwtSecurityToken(
				issuer: _issuer,
				audience: _aud,
				claims: new[] { new Claim("mid", m.MemberID.ToString()), new Claim("type", "refresh") },
				notBefore: now,
				expires: now.AddDays(_refreshDays),
				signingCredentials: creds
			);
			var refreshToken = new JwtSecurityTokenHandler().WriteToken(refresh);

			return (accessToken, refreshToken);
		}

		public string IssueTempToken(int memberId, int minutes, string purpose)
		{
			var now = DateTime.UtcNow;
			var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
			var claims = new[]
			{
				new Claim("mid", memberId.ToString()),
				new Claim("purpose", purpose)
			};
			var jwt = new JwtSecurityToken(
				issuer: _issuer,
				audience: _aud,
				claims: claims,
				notBefore: now,
				expires: now.AddMinutes(minutes),
				signingCredentials: creds
			);
			return new JwtSecurityTokenHandler().WriteToken(jwt);
		}

		public int? TryGetMemberIdFromTempToken(string authHeader)
		{
			if (string.IsNullOrWhiteSpace(authHeader)) return null;
			var parts = authHeader.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase)) return null;

			var handler = new JwtSecurityTokenHandler();
			try
			{
				var token = handler.ReadJwtToken(parts[1]);
				var purpose = token.Claims.FirstOrDefault(c => c.Type == "purpose")?.Value;
				if (string.IsNullOrEmpty(purpose)) return null;

				var mid = token.Claims.FirstOrDefault(c => c.Type == "mid")?.Value;
				return int.TryParse(mid, out var id) ? id : null;
			}
			catch { return null; }
		}
	}
}
