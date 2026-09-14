using System;
using System.Net;

namespace WPF_Fusion.Infrastructure
{
	/// <summary>
	/// Raised when a REST call cannot be completed. Carries enough detail for the
	/// UI to tell "server unreachable" apart from "session expired", which the previous
	/// silent null returns made impossible.
	/// </summary>
	public sealed class ApiException : Exception
	{
		public ApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
			: base(message, inner)
		{
			StatusCode = statusCode;
		}

		/// <summary>
		/// HTTP status returned by the server, or null if the request never got that far.
		/// </summary>
		public HttpStatusCode? StatusCode { get; }

		/// <summary>
		/// True when the server rejected the access token (expired or invalid).
		/// </summary>
		public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

		/// <summary>
		/// True when the server could not be reached at all.
		/// </summary>
		public bool IsNetworkFailure => StatusCode == null;

		/// <summary>
		/// Message suitable for display to the user.
		/// </summary>
		public static string Describe(Exception ex)
		{
			if (ex is ApiException api)
			{
				if (api.IsUnauthorized)
					return "Your session has expired. Please sign in again.";

				if (api.IsNetworkFailure)
					return "Cannot reach the server. Check that it is running and that " +
						   "ApiBaseUrl in App.config points to it.";

				return api.Message;
			}

			return ex.Message;
		}
	}
}
