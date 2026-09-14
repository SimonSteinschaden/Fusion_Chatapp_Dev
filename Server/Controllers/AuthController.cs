using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Infrastructure;
using Server.Models;

namespace Server.Controllers
{
	/// <summary>
	/// Provides authentication endpoints for user registration and login.
	/// On successful login, a JWT access token is issued.
	/// </summary>
	[ApiController]
	[Route("auth")]
	[EnableRateLimiting(Program.AuthRateLimitPolicy)]
	public sealed class AuthController : ControllerBase
	{
		/// <summary>Minimum password length accepted at registration.</summary>
		public const int MinPasswordLength = 8;

		private const int MinUsernameLength = 3;
		private const int MaxUsernameLength = 24;

		private static readonly Regex UsernamePattern =
			new Regex("^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

		private readonly AppDbContext _db;
		private readonly JwtTokenService _jwt;

		/// <summary>
		/// Creates a new instance of the AuthController.
		/// </summary>
		/// <param name="db">Entity Framework database context.</param>
		/// <param name="jwt">Service responsible for issuing JWT access tokens.</param>
		public AuthController(AppDbContext db, JwtTokenService jwt)
		{
			_db = db;
			_jwt = jwt;
		}

		/// <summary>
		/// Request payload for user registration.
		/// </summary>
		public sealed record RegisterRequest(string Email, string Username, string Password);

		/// <summary>
		/// Request payload for user login.
		/// </summary>
		public sealed record LoginRequest(string Email, string Password);

		/// <summary>
		/// Standard authentication response returned by register and login endpoints.
		/// </summary>
		/// <param name="Success">Indicates whether the operation succeeded.</param>
		/// <param name="ErrorMessage">Optional error message if the operation failed.</param>
		/// <param name="UserId">The authenticated user's unique identifier.</param>
		/// <param name="AccessToken">JWT access token (only set on successful login).</param>
		public sealed record AuthResponse(
			bool Success,
			string? ErrorMessage,
			Guid? UserId,
			string? AccessToken,
			string? Username);


		/// <summary>
		/// Registers a new user account.
		/// </summary>
		/// <remarks>
		/// - Email addresses must be unique.
		/// - Passwords are hashed server-side using PBKDF2.
		/// - No JWT token is issued during registration.
		/// </remarks>
		/// <param name="req">Registration request containing email and password.</param>
		/// <returns>
		/// 200 OK on success, 400 Bad Request on invalid input,
		/// 409 Conflict if the email is already registered.
		/// </returns>
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest req)
		{
			var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
			var username = (req.Username ?? string.Empty).Trim();
			var password = req.Password ?? string.Empty;

			if (string.IsNullOrWhiteSpace(email) ||
				string.IsNullOrWhiteSpace(username) ||
				string.IsNullOrWhiteSpace(password))
				return BadRequest(new AuthResponse(false, "Email, username and password are required.", null, null, null));

			// Enforced server-side: client-side validation alone is bypassed by calling
			// the API directly.
			if (password.Length < MinPasswordLength)
				return BadRequest(new AuthResponse(false,
					$"Password must be at least {MinPasswordLength} characters.", null, null, null));

			if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
				return BadRequest(new AuthResponse(false,
					$"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.", null, null, null));

			if (!UsernamePattern.IsMatch(username))
				return BadRequest(new AuthResponse(false,
					"Username may only contain letters, numbers and underscore.", null, null, null));

			if (await _db.Users.AnyAsync(u => u.Email == email))
				return Conflict(new AuthResponse(false, "Email already registered.", null, null, null));

			if (await _db.Users.AnyAsync(u => u.Username == username))
				return Conflict(new AuthResponse(false, "Username already taken.", null, null, null));

			var user = new User
			{
				Email = email,
				Username = username,
				PasswordHash = PasswordHasher.HashPassword(password),
				CreatedAtUtc = DateTime.UtcNow
			};

			_db.Users.Add(user);

			try
			{
				await _db.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				// Safety net in case of race conditions (unique index on Email/Username)
				return Conflict(new AuthResponse(false, "Email or username already registered.", null, null, null));
			}

			return Ok(new AuthResponse(true, null, user.Id, null, user.Username));
		}


		/// <summary>
		/// Authenticates a user and issues a JWT access token.
		/// </summary>
		/// <remarks>
		/// - Credentials are verified against the stored password hash.
		/// - On success, a short-lived JWT access token is returned.
		/// </remarks>
		/// <param name="req">Login request containing email and password.</param>
		/// <returns>
		/// 200 OK with JWT token on success,
		/// 400 Bad Request on invalid input,
		/// 401 Unauthorized on invalid credentials.
		/// </returns>
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest req)
		{
			var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
			var password = req.Password ?? string.Empty;

			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
				return BadRequest(new AuthResponse(false, "Email and password are required.", null, null, null));

			var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
			if (user == null)
				return Unauthorized(new AuthResponse(false, "Invalid credentials.", null, null, null));

			if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
				return Unauthorized(new AuthResponse(false, "Invalid credentials.", null, null, null));

			user.LastLoginAtUtc = DateTime.UtcNow;
			await _db.SaveChangesAsync();

			// Issue JWT access token
			var token = _jwt.CreateAccessToken(user);

			return Ok(new AuthResponse(true, null, user.Id, token, user.Username));
		}
	}
}
