namespace GuildArena.Application.Abstractions;

/// <summary>
/// Provides access to the currently authenticated user's identity and active game profile.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the unique identifier (GUID) of the authenticated ApplicationUser.
    /// Used for security, identity, and account management operations.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the unique identifier of the user's active Guild.
    /// This acts as the "PlayerId" across all combat and gameplay mechanics.
    /// </summary>
    int? GuildId { get; }
}