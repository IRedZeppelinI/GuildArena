using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Orchestrates end-of-combat logic: determines winner, applies rewards,
/// persists match history, and cleans up combat state.
/// </summary>
public interface ICombatResolutionService
{
    /// <summary>
    /// Resolves a finished combat session.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat.</param>
    /// <param name="state">The final game state.</param>
    /// <param name="userId">The ID of the requesting player.</param>
    /// <param name="isSurrender"><c>true</c> if the combat ended by surrender.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CombatResultDto> ResolveCombatAsync(string combatId, GameState state, string userId, bool isSurrender, CancellationToken ct);
}