namespace GuildArena.Shared.Requests;

/// <summary>
/// Represents the payload to finalize a password reset operation.
/// </summary>
public class ResetPasswordRequest
{
    /// <summary>
    /// The email address of the account.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// The secure authorization code provided via email.
    /// </summary>
    public required string ResetCode { get; set; }

    /// <summary>
    /// The new password for the account.
    /// </summary>
    public required string NewPassword { get; set; }
}