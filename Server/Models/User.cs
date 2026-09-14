using Server.Models.Chat;

namespace Server.Models
{
	public class User
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Email { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty;
		public string PasswordHash { get; set; } = string.Empty;

		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
		public DateTime? LastLoginAtUtc { get; set; }

		//GroupChats
		public ICollection<ClientDevice> Clients { get; set; } = new List<ClientDevice>();
		public ICollection<ConversationMember> ConversationMemberships { get; set; } = new List<ConversationMember>();

	}
}
