using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;

namespace WPF_Fusion.Service
{
    /// <summary>
    /// Client-side chat service communicating with the server REST API.
    /// Handles conversations and messages via HTTP.
    /// </summary>
    public sealed class ChatApiService : IChatService
    {

        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };



        public ChatApiService(HttpClient httpClient)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }


        // ----------------------------
        // Public API
        // ----------------------------

        // GET /conversations
        public async Task<IReadOnlyList<ConversationDto>> GetMyConversationsAsync()
        {
            var dto = await GetJsonAsync<List<ConversationDto>>("conversations");
            return dto ?? new List<ConversationDto>();
        }

        // GET /conversations/{id}/messages?take=50&skip=0
        public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, int take = 50, int skip = 0)
        {
            var path = $"conversations/{conversationId}/messages?take={take}&skip={skip}";
            var dto = await GetJsonAsync<List<MessageDto>>(path);
            return dto ?? new List<MessageDto>();
        }

		// GET /conversations/{id}/messages?afterUtc=...
		public async Task<IReadOnlyList<MessageDto>> GetMessagesAfterAsync(Guid conversationId, DateTime afterUtc)
		{
			// ISO 8601 "round-trip", immer UTC
			var after = Uri.EscapeDataString(afterUtc.ToUniversalTime().ToString("O"));

			var path = $"conversations/{conversationId}/messages?afterUtc={after}";
			var dto = await GetJsonAsync<List<MessageDto>>(path);

			return dto ?? new List<MessageDto>();
		}
		// POST /conversations/{id}/messages
		public async Task<MessageDto?> SendMessageAsync(Guid conversationId, string content)
        {
            var req = new SendMessageRequest { Content = content ?? "" };
            return await PostJsonAsync<SendMessageRequest, MessageDto>($"conversations/{conversationId}/messages", req);
        }

        // POST /conversations/direct
        public async Task<ConversationDto?> GetOrCreateDirectAsync(Guid otherUserId)
        {
            var req = new CreateDirectRequest { OtherUserId = otherUserId };
            return await PostJsonAsync<CreateDirectRequest, ConversationDto>("conversations/direct", req);
        }

        // POST /conversations/group
        public async Task<ConversationDto?> CreateGroupAsync(string title, IEnumerable<Guid> participantUserIds)
        {
            var req = new CreateGroupRequest
            {
                Title = title ?? "",
                ParticipantUserIds = participantUserIds == null
                    ? new List<Guid>()
                    : new List<Guid>(participantUserIds)
            };

            return await PostJsonAsync<CreateGroupRequest, ConversationDto>("conversations/group", req);
        }

        // ----------------------------
        // Request DTOs
        // ----------------------------

        private sealed class SendMessageRequest
        {
            public string Content { get; set; } = "";
        }

        private sealed class CreateDirectRequest
        {
            public Guid OtherUserId { get; set; }
        }

        private sealed class CreateGroupRequest
        {
            public string Title { get; set; } = "";
            public List<Guid> ParticipantUserIds { get; set; } = new();
        }

        // ----------------------------
        // HTTP Helpers
        // ----------------------------

        private async Task<T?> GetJsonAsync<T>(string path) where T : class
        {
            HttpResponseMessage resp;
            try
            {
                resp = await _http.GetAsync(path);
            }
            catch (Exception ex)
            {
                // Previously swallowed: an unreachable server looked exactly like
                // "no conversations".
                throw new ApiException("Request failed.", null, ex);
            }

            using (resp)
            {
                var body = await resp.Content.ReadAsStringAsync();

                EnsureAuthorized(resp);

                if (!resp.IsSuccessStatusCode)
                    throw new ApiException(
                        $"Server returned HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.",
                        resp.StatusCode);

                try
                {
                    return JsonSerializer.Deserialize<T>(body, JsonOptions);
                }
                catch (Exception ex)
                {
                    throw new ApiException($"Could not read the server response: {ex.Message}", resp.StatusCode, ex);
                }
            }
        }

        private async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string path, TRequest payload)
            where TResponse : class
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
                throw new ApiException("Request failed.", null, ex);
            }

            using (resp)
            {
                var body = await resp.Content.ReadAsStringAsync();

                EnsureAuthorized(resp);

                if (!resp.IsSuccessStatusCode)
                    throw new ApiException(
                        BuildErrorMessage(resp, body),
                        resp.StatusCode);

                TResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
                }
                catch (Exception ex)
                {
                    throw new ApiException($"Could not read the server response: {ex.Message}", resp.StatusCode, ex);
                }

                if (parsed == null)
                    throw new ApiException("The server returned an empty response.", resp.StatusCode);

                return parsed;
            }
        }

        /// <summary>
        /// Turns a rejected token into a session-expired notification so the shell can
        /// send the user back to the login screen.
        /// </summary>
        private static void EnsureAuthorized(HttpResponseMessage resp)
        {
            if (resp.StatusCode != HttpStatusCode.Unauthorized)
                return;

            Session.NotifyExpired();
            throw new ApiException("Session expired.", HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Prefers the server's plain-text reason over a bare status code.
        /// </summary>
        private static string BuildErrorMessage(HttpResponseMessage resp, string body)
        {
            var trimmed = (body ?? "").Trim();

            if (trimmed.Length > 0 && trimmed.Length < 200 && !trimmed.StartsWith("{") && !trimmed.StartsWith("<"))
                return trimmed;

            return $"Server returned HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.";
        }
    }
}