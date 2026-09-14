using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Interface;
using Server.Persistence;

/// <summary>
/// REST controller providing authenticated conversation and message endpoints.
/// Resolves usernames for participants and message senders for client display.
/// </summary>
[ApiController]
[Route("conversations")]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly IChatStore _chatStore;
    private readonly IUserStore _userStore; // Used to resolve usernames for DTOs.

    public ConversationsController(IChatStore chatStore, IUserStore userStore)
    {
        _chatStore = chatStore;
        _userStore = userStore;
    }

    /// <summary>
    /// Returns all conversations the current user participates in.
    /// Participant usernames are resolved for client display.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>> GetConversations()
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();

        var convos = await _chatStore.GetConversationsForUserAsync(me);

        // One lookup for all previews instead of one per conversation.
        var lastMessages = await _chatStore.GetLastMessagesAsync(convos.Select(c => c.ConversationId));

        var result = new List<ConversationDto>();
        foreach (var c in convos)
        {
            var dto = await ToDtoAsync(c, includeLastMessage: false);

            if (lastMessages.TryGetValue(c.ConversationId, out var last))
            {
                dto.LastMessage = new MessagePreviewDto
                {
                    Content = last.Content,
                    SentAtUtc = last.SentAtUtc,
                    SenderUserId = last.SenderUserId
                };
            }

            result.Add(dto);
        }

        // Most recently active conversation first; conversations without messages last.
        return Ok(result
            .OrderByDescending(r => r.LastMessage?.SentAtUtc ?? DateTime.MinValue)
            .ToList());
    }

    /// <summary>
    /// Request payload for creating or retrieving a direct conversation.
    /// </summary>
    public sealed class CreateDirectRequest
    {
        public Guid OtherUserId { get; set; }
    }

    /// <summary>
    /// Creates a direct conversation with another user or returns the existing one.
    /// </summary>
    [HttpPost("direct")]
    public async Task<ActionResult<ConversationDto>> CreateOrGetDirect([FromBody] CreateDirectRequest req)
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();
        if (req.OtherUserId == Guid.Empty || req.OtherUserId == me) return BadRequest();

        if (await _userStore.FindByIdAsync(req.OtherUserId) == null)
            return NotFound();

        var convo = await _chatStore.GetOrCreateDirectAsync(me, req.OtherUserId);

        return Ok(await ToDtoAsync(convo, includeLastMessage: true));
    }

    /// <summary>
    /// Request payload for creating a group conversation.
    /// </summary>
    public sealed class CreateGroupRequest
    {
        public string Title { get; set; } = "";
        public List<Guid> ParticipantUserIds { get; set; } = new();
    }

    /// <summary>
    /// Creates a group conversation. The caller is always added as a participant.
    /// A group needs at least three distinct members.
    /// </summary>
    [HttpPost("group")]
    public async Task<ActionResult<ConversationDto>> CreateGroup([FromBody] CreateGroupRequest req)
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();

        var participants = (req.ParticipantUserIds ?? new List<Guid>())
            .Append(me)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (participants.Count < 3)
            return BadRequest("A group needs at least three distinct participants.");

        // Reject unknown ids rather than creating a group with phantom members.
        foreach (var id in participants)
        {
            if (await _userStore.FindByIdAsync(id) == null)
                return BadRequest($"Unknown user: {id}");
        }

        var convo = await _chatStore.CreateGroupAsync(req.Title ?? "", participants);

        return Ok(await ToDtoAsync(convo, includeLastMessage: false));
    }

    /// <summary>
    /// Returns messages for a conversation with optional paging parameters.
    /// Sender usernames are included for UI convenience.
    /// </summary>
    [HttpGet("{conversationId:guid}/messages")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(
        Guid conversationId,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0,
        [FromQuery] DateTime? afterUtc = null)
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();

        var guard = await EnsureParticipantAsync(conversationId, me);
        if (guard != null) return guard;

        // afterUtc turns this into an incremental poll: clients ask only for what they
        // have not seen yet, instead of refetching the first page over and over.
        var msgs = afterUtc.HasValue
            ? await _chatStore.GetMessagesAfterAsync(conversationId, afterUtc.Value)
            : await _chatStore.GetMessagesAsync(conversationId, take, skip);

        return Ok(await ToDtosAsync(msgs));
    }

    /// <summary>
    /// Request payload for sending a message to a conversation.
    /// </summary>
    public sealed class SendMessageRequest
    {
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// Adds a message to the specified conversation as the current user.
    /// Returns the created message including sender username.
    /// </summary>
    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest req)
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();

        var guard = await EnsureParticipantAsync(conversationId, me);
        if (guard != null) return guard;

        var content = (req.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) return BadRequest("Empty message.");

        var msg = await _chatStore.AddMessageAsync(conversationId, me, content);
        var sender = await _userStore.FindByIdAsync(me);

        return Ok(new MessageDto
        {
            MessageId = msg.MessageId,
            ConversationId = msg.ConversationId,
            SenderUserId = msg.SenderUserId,
            Content = msg.Content,
            SentAtUtc = msg.SentAtUtc,
            SenderUsername = sender?.Username
        });
    }

    // ----------------------------
    // DTOs
    // ----------------------------

    /// <summary>
    /// Conversation DTO returned to clients, including resolved participants.
    /// </summary>
    public sealed class ConversationDto
    {
        public Guid ConversationId { get; set; }
        public bool IsGroup { get; set; }
        public string? Title { get; set; }
        public List<UserDto> Participants { get; set; } = new();

        /// <summary>Newest message, for the conversation list preview. Null if empty.</summary>
        public MessagePreviewDto? LastMessage { get; set; }
    }

    /// <summary>
    /// Compact form of the newest message, used for conversation list previews.
    /// </summary>
    public sealed class MessagePreviewDto
    {
        public string Content { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public Guid SenderUserId { get; set; }
    }

    /// <summary>
    /// User DTO used in conversation participant lists.
    /// </summary>
    public sealed class UserDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
    }

    /// <summary>
    /// Message DTO returned to clients, optionally including the sender username.
    /// </summary>
    public sealed class MessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public string? SenderUsername { get; set; }
    }

    /// <summary>
    /// Maps a conversation record to its DTO, resolving participant usernames.
    /// </summary>
    private async Task<ConversationDto> ToDtoAsync(ConversationRecord convo, bool includeLastMessage)
    {
        var participants = new List<UserDto>();

        foreach (var uid in convo.ParticipantUserIds.Distinct())
        {
            var u = await _userStore.FindByIdAsync(uid);
            if (u != null)
                participants.Add(new UserDto { UserId = u.UserId, Username = u.Username });
        }

        var dto = new ConversationDto
        {
            ConversationId = convo.ConversationId,
            IsGroup = convo.IsGroup,
            Title = convo.Title,
            Participants = participants
        };

        if (includeLastMessage)
        {
            var last = await _chatStore.GetLastMessagesAsync(new[] { convo.ConversationId });
            if (last.TryGetValue(convo.ConversationId, out var m))
            {
                dto.LastMessage = new MessagePreviewDto
                {
                    Content = m.Content,
                    SentAtUtc = m.SentAtUtc,
                    SenderUserId = m.SenderUserId
                };
            }
        }

        return dto;
    }

    /// <summary>
    /// Maps message records to DTOs, resolving each sender's username once rather than
    /// per message.
    /// </summary>
    private async Task<List<MessageDto>> ToDtosAsync(IReadOnlyList<MessageRecord> msgs)
    {
        var usernames = new Dictionary<Guid, string?>();

        foreach (var senderId in msgs.Select(m => m.SenderUserId).Distinct())
            usernames[senderId] = (await _userStore.FindByIdAsync(senderId))?.Username;

        return msgs.Select(m => new MessageDto
        {
            MessageId = m.MessageId,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            Content = m.Content,
            SentAtUtc = m.SentAtUtc,
            SenderUsername = usernames.TryGetValue(m.SenderUserId, out var name) ? name : null
        }).ToList();
    }

    /// <summary>
    /// Verifies that <paramref name="userId"/> participates in the conversation.
    /// A valid token alone must never grant access to someone else's conversation.
    /// </summary>
    /// <returns>
    /// null when access is allowed; otherwise the error result to return.
    /// Non-participants get 404 rather than 403 so the endpoint does not reveal
    /// whether a given conversation id exists.
    /// </returns>
    private async Task<ActionResult?> EnsureParticipantAsync(Guid conversationId, Guid userId)
    {
        var convo = await _chatStore.GetConversationAsync(conversationId);

        if (convo?.ParticipantUserIds == null || !convo.ParticipantUserIds.Contains(userId))
            return NotFound();

        return null;
    }

    /// <summary>
    /// Extracts the authenticated user id from common JWT claim keys.
    /// Returns Guid.Empty if no valid id is present.
    /// </summary>
    private static Guid GetUserIdFromClaims(ClaimsPrincipal user)
    {
        // Robust: accepts multiple common claim keys.
        var raw =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("userId") ??
            user.FindFirstValue("UserId");

        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}