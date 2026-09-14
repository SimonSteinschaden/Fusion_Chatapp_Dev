using System.Threading.Tasks;
using WPF_Fusion.MVVM.Model;

namespace WPF_Fusion.Interface
{
    /// <summary>
    /// Defines authentication operations for login and registration.
    /// Implementations communicate with the server and return structured login results.
    /// </summary>
    public interface IAuthService
    {
        Task<LoginResult> LoginAsync(string email, string password);

        // UserId is created by the server; the client submits the username.
        Task<LoginResult> RegisterAsync(string email, string password, string username);
    }
}