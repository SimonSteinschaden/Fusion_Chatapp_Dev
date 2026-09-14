using System.Text;
using Newtonsoft.Json;
using Server.Persistence;
using Server.Interface;

namespace Server.Service
{
	public sealed class JsonUserStore : IUserStore
	{
		private readonly string _filePath;
		private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

		public JsonUserStore(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				throw new ArgumentException("File path is required.", nameof(filePath));

			_filePath = filePath;
		}

		public async Task<UserRecord?> FindByEmailAsync(string email)
		{
			var normalized = NormalizeEmail(email);

			await _mutex.WaitAsync().ConfigureAwait(false);
			try
			{
				var users = await LoadAllAsync().ConfigureAwait(false);
				return users.FirstOrDefault(u => NormalizeEmail(u.Email) == normalized);
			}
			finally
			{
				_mutex.Release();
			}
		}

		public async Task<UserRecord?> FindByIdAsync(Guid userId)
		{
			if (userId == Guid.Empty) return null;

			await _mutex.WaitAsync().ConfigureAwait(false);
			try
			{
				var users = await LoadAllAsync().ConfigureAwait(false);
				return users.FirstOrDefault(u => u.UserId == userId);
			}
			finally
			{
				_mutex.Release();
			}
		}

		public async Task<UserRecord?> FindByUsernameAsync(string username)
		{
			var normalized = NormalizeUsername(username);

			await _mutex.WaitAsync().ConfigureAwait(false);
			try
			{
				var users = await LoadAllAsync().ConfigureAwait(false);
				return users.FirstOrDefault(u => NormalizeUsername(u.Username) == normalized);
			}
			finally
			{
				_mutex.Release();
			}
		}

		/// <summary>
		/// Creates a new user if email and username are not taken.
		/// Expects UserId + Username to already be set by caller (server).
		/// Returns false if email or username already exists.
		/// </summary>
		public async Task<bool> CreateAsync(UserRecord user)
		{
			if (user == null) throw new ArgumentNullException(nameof(user));

			var emailNorm = NormalizeEmail(user.Email);
			var usernameNorm = NormalizeUsername(user.Username);

			if (string.IsNullOrWhiteSpace(emailNorm))
				throw new ArgumentException("Email is required.", nameof(user));

			if (string.IsNullOrWhiteSpace(usernameNorm))
				throw new ArgumentException("Username is required.", nameof(user));

			if (user.UserId == Guid.Empty)
				throw new ArgumentException("UserId must be set (server generated).", nameof(user));

			await _mutex.WaitAsync().ConfigureAwait(false);
			try
			{
				var users = await LoadAllAsync().ConfigureAwait(false);

				// Unique constraints
				if (users.Any(u => NormalizeEmail(u.Email) == emailNorm))
					return false;

				if (users.Any(u => NormalizeUsername(u.Username) == usernameNorm))
					return false;

				users.Add(new UserRecord
				{
					UserId = user.UserId,
					Email = emailNorm,
					PasswordHash = user.PasswordHash ?? "",
					Username = user.Username.Trim()
				});

				await SaveAllAsync(users).ConfigureAwait(false);
				return true;
			}
			finally
			{
				_mutex.Release();
			}
		}

		private async Task<List<UserRecord>> LoadAllAsync()
		{
			if (!File.Exists(_filePath))
				return new List<UserRecord>();

			string json;
			using (var reader = new StreamReader(_filePath, Encoding.UTF8))
			{
				json = await reader.ReadToEndAsync().ConfigureAwait(false);
			}

			if (string.IsNullOrWhiteSpace(json))
				return new List<UserRecord>();

			try
			{
				var users = JsonConvert.DeserializeObject<List<UserRecord>>(json);
				return users ?? new List<UserRecord>();
			}
			catch (JsonException)
			{
				// if file is corrupt, return empty instead of crashing
				return new List<UserRecord>();
			}
		}

		private async Task SaveAllAsync(List<UserRecord> users)
		{
			if (users == null) throw new ArgumentNullException(nameof(users));

			var dir = Path.GetDirectoryName(_filePath);
			if (!string.IsNullOrWhiteSpace(dir))
				Directory.CreateDirectory(dir);

			var json = JsonConvert.SerializeObject(users, Formatting.Indented);

			using (var writer = new StreamWriter(_filePath, false, Encoding.UTF8))
			{
				await writer.WriteAsync(json).ConfigureAwait(false);
			}
		}

		private static string NormalizeEmail(string email)
			=> (email ?? "").Trim().ToLowerInvariant();

		private static string NormalizeUsername(string username)
			=> (username ?? "").Trim().ToLowerInvariant();
	}
}
