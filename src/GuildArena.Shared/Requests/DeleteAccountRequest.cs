namespace GuildArena.Shared.Requests;

/// <summary>
/// Represents the payload for permanently deleting a user account.
/// Requires the current password to prevent unauthorized deletions.
/// </summary>
public class DeleteAccountRequest
{
    /// <summary>
    /// The user's current password for security verification.
    /// </summary>
    public required string Password { get; set; }
}