using System;

namespace WPF_Fusion.Infrastructure
{
	/// <summary>
	/// Holds the current in-memory session state (JWT access token, UserId, Username).
	/// Cleared on logout; never persisted to disk.
	/// </summary>
	public static class Session
	{
		public static string? AccessToken { get; set; }
		public static Guid? UserId { get; set; }
		public static string? Username { get; set; }

		/// <summary>
		/// True once a successful login has stored an access token.
		/// </summary>
		public static bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

		/// <summary>
		/// Raised when the server rejects the access token, so the shell can return
		/// the user to the login screen instead of silently showing empty lists.
		/// </summary>
		public static event EventHandler? Expired;

		/// <summary>
		/// Discards all session state. Called on logout and when a token is rejected.
		/// </summary>
		public static void Clear()
		{
			AccessToken = null;
			UserId = null;
			Username = null;
		}

		/// <summary>
		/// Clears the session and notifies listeners that re-authentication is required.
		/// Safe to call repeatedly; only the first call after a successful login notifies.
		/// </summary>
		public static void NotifyExpired()
		{
			if (!IsAuthenticated)
				return;

			Clear();
			Expired?.Invoke(null, EventArgs.Empty);
		}
	}
}
