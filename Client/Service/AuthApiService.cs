using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;
using WPF_Fusion.MVVM.Model;

namespace WPF_Fusion.Service
{
    /// <summary>
    /// Client-side authentication service calling the server REST API.
    /// Password hashing is performed on the server only.
    /// </summary>
    public sealed class AuthApiService : IAuthService
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AuthApiService(HttpClient httpClient)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        // ----------------------------
        // DTOs
        // ----------------------------

        private sealed class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        private sealed class RegisterRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
        }

        private sealed class AuthResponse
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }

            // Optional: user id returned by the server (if provided).
            public Guid? UserId { get; set; }

            // JWT returned by the server on login (optional on register).
            public string? AccessToken { get; set; }
            public string? Username { get; set; }
        }

        // ----------------------------
        // Public API
        // ----------------------------

        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            var req = new LoginRequest
            {
                Email = (email ?? string.Empty).Trim(),
                Password = password ?? string.Empty
            };

            var dto = await PostJsonAsync<LoginRequest, AuthResponse>("auth/login", req);

            if (!dto.Success || string.IsNullOrWhiteSpace(dto.AccessToken))
                return LoginResult.Fail(dto.ErrorMessage ?? "Invalid credentials.");

            // Set token (must be set before calling /users/me).
            Session.AccessToken = dto.AccessToken;
            Session.Username = dto.Username;

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Session.AccessToken);

            // Optional: take user id from login response (if present).
            Session.UserId = dto.UserId;

            // Fetch username (and user id) from server as the authoritative source.
            var me = await App.UserApiService.GetMeAsync();
            if (me != null && !string.IsNullOrWhiteSpace(me.Username))
            {
                Session.UserId = me.UserId;
                Session.Username = me.Username;
            }
            return LoginResult.Ok();
        }

        public async Task<LoginResult> RegisterAsync(string email, string password, string username)
        {
            var req = new RegisterRequest
            {
                Email = (email ?? string.Empty).Trim(),
                Password = password ?? string.Empty,
                Username = (username ?? string.Empty).Trim()
            };

            var dto = await PostJsonAsync<RegisterRequest, AuthResponse>("auth/register", req);

            return dto.Success
                ? LoginResult.Ok()
                : LoginResult.Fail(dto.ErrorMessage ?? "Registration failed.");
        }

        // ----------------------------
        // HTTP helper
        // ----------------------------

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string path, TRequest payload)
            where TResponse : new()
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync(path, content);
            }
            catch (Exception ex)
            {
                if (typeof(TResponse) == typeof(AuthResponse))
                {
                    return (TResponse)(object)new AuthResponse
                    {
                        Success = false,
                        ErrorMessage = $"Request failed: {ex.Message}"
                    };
                }

                return new TResponse();
            }

			using (resp)
			{
				var body = await resp.Content.ReadAsStringAsync();

				// For authentication responses, always try to deserialize the body,
				// even if the status code is not successful.
				if (typeof(TResponse) == typeof(AuthResponse))
				{
					try
					{
						var dto = JsonSerializer.Deserialize<AuthResponse>(body, JsonOptions);
						if (dto != null)
						{
							// Ensure Success is false if HTTP status is not successful.
							if (!resp.IsSuccessStatusCode)
								dto.Success = false;

							// Provide a fallback error message if none was supplied.
							dto.ErrorMessage ??= resp.IsSuccessStatusCode
								? null
								: $"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}";

							return (TResponse)(object)dto;
						}
					}
					catch
					{
						// Ignore JSON parsing errors and fall back below.
					}

					return (TResponse)(object)new AuthResponse
					{
						Success = false,
						ErrorMessage = !resp.IsSuccessStatusCode
							? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}"
							: "Invalid response."
					};
				}

				// For all other response types, treat non-success status codes as fatal.
				if (!resp.IsSuccessStatusCode)
					throw new InvalidOperationException(
						$"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");

				try
				{
					var dto = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
					if (dto != null)
						return dto;
				}
				catch
				{
					// Ignore JSON parsing errors and return a default instance.
				}

				return new TResponse();
			}
		}
    }
}