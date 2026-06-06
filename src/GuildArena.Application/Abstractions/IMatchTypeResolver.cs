using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Defines a resolver for a specific match type that handles reward calculation,
/// persistence of match history, and any side-effects like dungeon progression.
/// </summary>
public interface IMatchTypeResolver
{
    /// <summary>
    /// Indicates whether this resolver can handle the given match type.
    /// </summary>
    // MatchType faz conflict com MatchType de IO, FQDN para evitar conflito
    bool CanHandle(GuildArena.Domain.Enums.Matches.MatchType matchType);

    /// <summary>
    /// Resolves the combat outcome for the given match type.
    /// Applies rewards, updates guild state, persists match history, and returns the result DTO.
    /// Note: Deletion of Redis combat state is handled by the caller after resolution.
    /// </summary>
    /// <param name="combatId">The unique combat ID (unused by resolver, kept for consistency).</param>
    /// <param name="state">Final game state.</param>
    /// <param name="userId">The requesting user ID.</param>
    /// <param name="isSurrender">Whether the combat ended by surrender.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The combat result DTO.</returns>
    Task<CombatResultDto> ResolveMatchAsync(
        string combatId,
        GameState state,
        string userId,
        bool isSurrender,
        CancellationToken ct);
}