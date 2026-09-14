using System;
using System.Threading;
using System.Threading.Tasks;

namespace WPF_Fusion.Interface
{
    /// <summary>
    /// Defines user-related lookup and identity operations.
    /// Implementations communicate with the backend to resolve usernames and retrieve the current user.
    /// </summary>
    public interface IUserService
	{
		Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
		Task<Guid?> GetUserIdByUsernameAsync(string username);
		Task<MeDto?> GetMeAsync();

	}

}
