using Server.Persistence;

namespace Server.Interface
{
	public interface IChatStore
	{
		Task<ConversationRecord?> GetConversationAsync(Guid conversationId);

		// get all conversations user participates
		Task<List<ConversationRecord>> GetConversationsForUserAsync(Guid userId);

		// find or create DM-Chats
		Task<ConversationRecord> GetOrCreateDirectAsync(Guid userA, Guid userB);

		// Create Groupchat
		Task<ConversationRecord> CreateGroupAsync(string title, List<Guid> participants);

		/// <summary>
		/// Returns the most recent <paramref name="take"/> messages of a conversation
		/// in chronological order. <paramref name="skip"/> pages further into the past.
		/// </summary>
		Task<List<MessageRecord>> GetMessagesAsync(Guid conversationId, int take = 50, int skip = 0);

		/// <summary>
		/// Returns messages sent strictly after <paramref name="afterUtc"/>, oldest first.
		/// Used by clients to poll for new messages without refetching the whole history.
		/// </summary>
		Task<List<MessageRecord>> GetMessagesAfterAsync(Guid conversationId, DateTime afterUtc, int take = 200);

		/// <summary>
		/// Returns the newest message per conversation, for conversation list previews.
		/// Conversations without messages are absent from the result.
		/// </summary>
		Task<Dictionary<Guid, MessageRecord>> GetLastMessagesAsync(IEnumerable<Guid> conversationIds);

		Task<MessageRecord> AddMessageAsync(Guid conversationId, Guid senderUserId, string content);
	}

}
