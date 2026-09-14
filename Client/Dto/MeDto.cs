using System;

/// <summary>
/// Represents the currently authenticated user returned by the backend.
/// </summary>
public sealed class MeDto
{
	public Guid UserId { get; set; }
	public string Username { get; set; } = "";
	public string Email { get; set; } = "";
}
