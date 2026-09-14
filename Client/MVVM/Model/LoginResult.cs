

namespace WPF_Fusion.MVVM.Model
{
    /// <summary>
    /// Represents the outcome of a login or registration operation.
    /// Provides factory methods for success and failure results.
    /// </summary>
    public sealed class LoginResult
	{
		public bool Success { get; }
		public string? ErrorMessage { get; }

		private LoginResult(bool success, string? errorMessage)
		{
			Success = success;
			ErrorMessage = errorMessage;
		}

		public static LoginResult Ok() => new(true, null);
		public static LoginResult Fail(string message) => new(false, message);
	}

}
