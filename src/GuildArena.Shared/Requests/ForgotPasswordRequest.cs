namespace GuildArena.Shared.Requests;

/// <summary>
/// Represents the payload for a password reset request.
/// </summary>
public class ForgotPasswordRequest
{
    /// <summary>
    /// The email address associated with the account.
    /// </summary>
    public required string Email { get; set; }
}