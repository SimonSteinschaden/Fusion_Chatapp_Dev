using System;
using System.Security.Cryptography;

namespace Server.Infrastructure
{
	/// <summary>
	/// Provides secure password hashing and verification using PBKDF2 (HMAC-SHA256).
	/// Compatible with .NET Framework 4.8.
	/// </summary>
	public static class PasswordHasher
	{
		// Recommended range: 100k–600k iterations depending on target hardware
		private const int SaltSize = 16;      // 128-bit salt
		private const int KeySize = 32;       // 256-bit derived key
		private const int Iterations = 210_000;

		/// <summary>
		/// Hashes a plaintext password using PBKDF2 with a random salt.
		/// </summary>
		/// <param name="password">The plaintext password.</param>
		/// <returns>
		/// A formatted hash string:
		/// PBKDF2$&lt;iterations&gt;$&lt;base64Salt&gt;$&lt;base64Hash&gt;
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="password"/> is null.
		/// </exception>
		public static string HashPassword(string password)
		{
			if (password == null)
				throw new ArgumentNullException(nameof(password));

			// Generate a cryptographically secure random salt
			var salt = new byte[SaltSize];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(salt);
			}

			byte[] hash;

			// PBKDF2 implementation for .NET Framework 4.8
			using (var pbkdf2 = new Rfc2898DeriveBytes(
				password,
				salt,
				Iterations,
				HashAlgorithmName.SHA256))
			{
				hash = pbkdf2.GetBytes(KeySize);
			}

			return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
		}

		/// <summary>
		/// Verifies a plaintext password against a stored PBKDF2 hash.
		/// </summary>
		/// <param name="password">The plaintext password to verify.</param>
		/// <param name="stored">The stored hash string.</param>
		/// <returns>
		/// True if the password matches the stored hash; otherwise false.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="password"/> is null.
		/// </exception>
		public static bool VerifyPassword(string password, string stored)
		{
			if (password == null)
				throw new ArgumentNullException(nameof(password));

			if (string.IsNullOrWhiteSpace(stored))
				return false;

			// Expected format: PBKDF2$iterations$salt$hash
			var parts = stored.Split('$');
			if (parts.Length != 4)
				return false;

			if (!string.Equals(parts[0], "PBKDF2", StringComparison.Ordinal))
				return false;

			if (!int.TryParse(parts[1], out int iterations))
				return false;

			byte[] salt;
			byte[] expectedHash;

			try
			{
				salt = Convert.FromBase64String(parts[2]);
				expectedHash = Convert.FromBase64String(parts[3]);
			}
			catch
			{
				return false;
			}

			byte[] actualHash;

			using (var pbkdf2 = new Rfc2898DeriveBytes(
				password,
				salt,
				iterations,
				HashAlgorithmName.SHA256))
			{
				actualHash = pbkdf2.GetBytes(expectedHash.Length);
			}

			return FixedTimeEquals(actualHash, expectedHash);
		}

		/// <summary>
		/// Compares two byte arrays in constant time to prevent timing attacks.
		/// </summary>
		/// <param name="a">First byte array.</param>
		/// <param name="b">Second byte array.</param>
		/// <returns>
		/// True if both arrays are equal; otherwise false.
		/// </returns>
		private static bool FixedTimeEquals(byte[] a, byte[] b)
		{
			if (a == null || b == null)
				return false;

			// Do not early-exit on length mismatch (timing attack protection)
			int diff = a.Length ^ b.Length;
			int length = Math.Min(a.Length, b.Length);

			for (int i = 0; i < length; i++)
			{
				diff |= a[i] ^ b[i];
			}

			return diff == 0;
		}
	}
}
