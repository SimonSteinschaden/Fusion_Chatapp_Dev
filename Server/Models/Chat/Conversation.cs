namespace Server.Models.Chat
{
	public class Conversation
	{
		public Guid Id { get; set; }
		public string Type { get; set; } = "group";//enum möglich
		public string? Title { get; set; }

		public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
		public ICollection<Message> Messages { get; set; } = new List<Message>();
	}

	public class ConversationMember
	{
		public Guid ConversationId { get; set; }
		public Conversation Conversation { get; set; } = null!;
		
		public Guid UserId { get; set; }
		public User User { get; set; } = null!;//User required

		public string Role { get; set; } = "member";//enum möglich, member,admin, vip, whatsoever
		public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
	}

    /// <summary>
    /// Chat message persisted in the database and linked to a conversation and sender.
    /// </summary>
    public class Message
	{
		public Guid Id { get; set; }

		public Guid ConversationId { get; set; }
		public Conversation? Conversation { get; set; }
		public Guid SenderUserId { get; set; }
		public User? SenderUser { get; set; }

		public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
		public string Content { get; set; } = string.Empty;
	}
}
