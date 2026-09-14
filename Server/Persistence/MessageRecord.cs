namespace Server.Persistence
{
	public sealed class MessageRecord
	{
		public Guid MessageId { get; set; }
		public Guid ConversationId { get; set; }
		public Guid SenderUserId { get; set; }

		public string Content { get; set; } = "";
		public DateTime SentAtUtc { get; set; }
	} 
}
