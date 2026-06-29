namespace GuildArena.Shared.Requests;

/// <summary>
/// Payload to request a new email verification link from the Identity framework.
/// </summary>
public class ResendConfirmationEmailRequest
{
    /// <summary>
    /// The registered email address of the account.
    /// </summary>
    public required string Email { get; set; }
}