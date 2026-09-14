namespace Server.Persistence
{
	public sealed class ConversationRecord
	{
		public Guid ConversationId { get; set; }
		public bool IsGroup { get; set; }

		public string? Title { get; set; } // Gruppenname optional

		public List<Guid> ParticipantUserIds { get; set; } = new();
	}

}
