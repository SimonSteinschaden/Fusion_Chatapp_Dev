using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Interface;
using System.Security.Claims;

/// <summary>
/// REST controller providing authenticated user-related endpoints.
/// Supports retrieving the current user ("me") and resolving usernames to user ids.
/// </summary>
[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserStore _userStore;

    public UsersController(IUserStore userStore)
    {
        _userStore = userStore;
    }

    /// <summary>
    /// DTO returned by the /users/me endpoint for the currently authenticated user.
    /// </summary>
    public sealed class MeDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Returns the currently authenticated user (authoritative server identity).
    /// Uses the JWT claims to identify the caller.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> GetMe()
    {
        var me = GetUserIdFromClaims(User);
        if (me == Guid.Empty) return Unauthorized();

        var user = await _userStore.FindByIdAsync(me);
        if (user == null) return NotFound();

        return Ok(new MeDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email
        });
    }

    /// <summary>
    /// Extracts the authenticated user id from common JWT claim keys.
    /// Returns Guid.Empty if no valid id is present.
    /// </summary>
    private static Guid GetUserIdFromClaims(ClaimsPrincipal user)
    {
        // Robust: accepts multiple common claim keys.
        var raw =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("userId") ??
            user.FindFirstValue("UserId");

        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Resolves a username to its corresponding user id.
    /// Returns 404 if the username does not exist.
    /// </summary>
    [HttpGet("by-username/{username}")]
    public async Task<IActionResult> GetByUsername(string username)
    {
        username = (username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest();

        var user = await _userStore.FindByUsernameAsync(username);
        if (user == null)
            return NotFound();

        return Ok(new { userId = user.UserId });
    }

    /// <summary>
    /// Checks whether a username exists.
    /// Returns an object { exists = true/false }.
    /// </summary>
    [HttpGet("exists/{username}")]
    public async Task<IActionResult> Exists(string username)
    {
        username = (username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username))
            return Ok(new { exists = false });

        var user = await _userStore.FindByUsernameAsync(username);
        return Ok(new { exists = user != null });
    }
}