namespace Server.Persistence
{
	public class UserRecord
	{
		public Guid UserId { get; set; }          // server generates GUID
		public string Email { get; set; } = "";
		public string PasswordHash { get; set; } = "";
		public string Username { get; set; } = ""; // unique
	}
}
