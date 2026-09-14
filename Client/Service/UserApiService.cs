using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;

/// <summary>
/// Client-side user service communicating with the server REST API.
/// Provides user lookup utilities and the current user endpoint.
/// </summary>
public class UserApiService : IUserService
{
    private readonly HttpClient _http;
    public UserApiService(HttpClient http) => _http = http;

    // ----------------------------
    // Public API
    // ----------------------------

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        var res = await SendAsync($"/users/exists/{Uri.EscapeDataString(username)}", ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return false;

        EnsureSuccess(res);

        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ExistsResponse>(json, JsonOptions)?.Exists ?? false;
    }

    // Look up a user id by username. Returns null if the username is empty or not found.
    public async Task<Guid?> GetUserIdByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var res = await SendAsync($"/users/by-username/{Uri.EscapeDataString(username)}");

        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(res);

        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UserIdResponse>(json, JsonOptions)?.UserId;
    }

    public async Task<MeDto?> GetMeAsync()
    {
        var res = await SendAsync("/users/me");

        if (!res.IsSuccessStatusCode)
            return null;

        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MeDto>(json, JsonOptions);
    }

    // ----------------------------
    // HTTP helpers
    // ----------------------------

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct = default)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.GetAsync(path, ct);
        }
        catch (Exception ex)
        {
            throw new ApiException("Request failed.", null, ex);
        }

        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            Session.NotifyExpired();
            throw new ApiException("Session expired.", HttpStatusCode.Unauthorized);
        }

        return res;
    }

    private static void EnsureSuccess(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            throw new ApiException(
                $"Server returned HTTP {(int)res.StatusCode} {res.ReasonPhrase}.",
                res.StatusCode);
    }

    // ----------------------------
    // Response DTOs
    // ----------------------------

    private sealed class ExistsResponse
    {
        public bool Exists { get; set; }
    }

    private sealed class UserIdResponse
    {
        public Guid UserId { get; set; }
    }
}