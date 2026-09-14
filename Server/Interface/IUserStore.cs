using Server.Persistence;

namespace Server.Interface
{
    public interface IUserStore
	{
		Task<UserRecord?> FindByEmailAsync(string email);
		Task<UserRecord?> FindByIdAsync(Guid userId);
		Task<UserRecord?> FindByUsernameAsync(string username);
		Task<bool> CreateAsync(UserRecord user); // returns false if email or user already exists
	}
}
