namespace Server.Models
{
	public class ClientDevice
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public Guid UserId { get; set; }
		public User? User { get; set; }

		public string ClientId { get; set; } = string.Empty; //user can have multiple devices
		public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
		public DateTime? LastSeenAtUtc { get; set; }
	}
}
