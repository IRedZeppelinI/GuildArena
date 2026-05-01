namespace GuildArena.Application.Abstractions;

/// <summary>
/// Provides access to the currently authenticated user's identity.
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
    /// </summary>
    int? GuildId { get; }
}