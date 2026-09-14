using System.Threading.Tasks;

namespace WPF_Fusion.Interface
{
    /// <summary>
    /// Defines server connectivity and client registration operations.
    /// Implementations handle connection setup and client validation against the backend.
    /// </summary>
    public interface IServerService
    {
        // Opens a server connection and performs authentication if required.
        Task<ServerConnectResult> ConnectAsync();

        // Checks whether the specified client is already registered on the server.
        Task<ClientStatusResult> GetClientStatusAsync(string clientId);

        // Registers the specified client on the server.
        Task<ServerActionResult> RegisterClientAsync(string clientId);
    }

    // ----------------------------
    // Result Models
    // ----------------------------

    /// <summary>
    /// Represents the outcome of a server connection attempt.
    /// </summary>
    public sealed class ServerConnectResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents the result of a client status check.
    /// </summary>
    public sealed class ClientStatusResult
    {
        public bool Success { get; set; }
        public bool IsRegistered { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents the result of a server-side action such as registration.
    /// </summary>
    public sealed class ServerActionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}