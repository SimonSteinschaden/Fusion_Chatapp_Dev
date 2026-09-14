using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WPF_Fusion.Interface
{
	/// <summary>
	/// Defines chat-related operations for conversations and messages.
	/// Implementations communicate with the server REST API.
	/// </summary>
	public interface IChatService
	{
		Task<IReadOnlyList<ConversationDto>> GetMyConversationsAsync();
		Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, int take = 50, int skip = 0);
		Task<IReadOnlyList<MessageDto>> GetMessagesAfterAsync(Guid conversationId, DateTime afterUtc); // <-- NEU
		Task<MessageDto?> SendMessageAsync(Guid conversationId, string content);
		Task<ConversationDto?> GetOrCreateDirectAsync(Guid otherUserId);
		Task<ConversationDto?> CreateGroupAsync(string title, IEnumerable<Guid> participantUserIds);
	}

	// --- DTOs ---

	public sealed class ConversationDto
    {
        public Guid ConversationId { get; set; }
        public bool IsGroup { get; set; }
        public string? Title { get; set; }

        // Participants used for client display
        public List<UserDto> Participants { get; set; } = new();

        public MessagePreviewDto? LastMessage { get; set; }
    }

    // TODO: Move to dedicated DTO namespace
    public sealed class UserDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
    }

    public sealed class MessagePreviewDto
    {
        public string Content { get; set; } = "";
        public DateTime SentAtUtc { get; set; }
        public Guid SenderUserId { get; set; }
    }

    public sealed class MessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }

        public string Content { get; set; } = "";
        public DateTime SentAtUtc { get; set; }

        // Optional: Username included for UI convenience
        public string? SenderUsername { get; set; }
    }
}