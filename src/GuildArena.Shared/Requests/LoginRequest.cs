namespace GuildArena.Shared.Requests;

/// <summary>
/// Payload to authenticate via ASP.NET Core Identity API Endpoints.
/// </summary>
public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}