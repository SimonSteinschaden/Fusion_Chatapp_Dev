using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Server.Models;

namespace Server.Infrastructure
{
	/// <summary>
	/// Issues short-lived JWT access tokens for authenticated users.
	/// </summary>
	public sealed class JwtTokenService
	{
		private readonly IConfiguration _cfg;
		public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

		public string CreateAccessToken(User user)
		{
			var issuer = _cfg["Jwt:Issuer"];
			var audience = _cfg["Jwt:Audience"];
			var key = _cfg["Jwt:Key"];
			var expiresMinutes = int.Parse(_cfg["Jwt:ExpiresMinutes"] ?? "120");

			var claims = new[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				new Claim(JwtRegisteredClaimNames.Email, user.Email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));
			var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
				signingCredentials: creds);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}
