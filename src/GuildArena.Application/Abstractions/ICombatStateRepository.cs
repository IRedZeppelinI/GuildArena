using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Defines the contract for persisting the state of an active combat.
/// The implementation (e.g., Redis) will live in the Infrastructure layer.
/// </summary>
public interface ICombatStateRepository
{
    /// <summary>
    /// Gets the full combat state from persistence.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat (e.g., GUID).</param>
    /// <returns>The deserialized GameState, or null if not found.</returns>
    Task<GameState?> GetAsync(string combatId);

    /// <summary>
    /// Saves (or overwrites) the full combat state.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat.</param>
    /// <param name="state">The GameState object to be serialized and saved.</param>
    Task SaveAsync(string combatId, GameState state);

    /// <summary>
    /// Removes a combat state from persistence (e.g., at the end of combat).
    /// </summary>
    /// <param name="combatId">The unique ID of the combat.</param>
    Task DeleteAsync(string combatId);

    /// <summary>
    /// Associates the user ID with an active combat.
    /// </summary>
    Task SetPlayerActiveCombatAsync(string userId, string combatId);

    /// <summary>
    /// Returns the combat ID the user is in, if one exists.
    /// </summary>
    Task<string?> GetPlayerActiveCombatAsync(string userId);

    /// <summary>
    /// Removes the association between the user and the combat (when it ends or the user quits).
    /// </summary>
    Task ClearPlayerActiveCombatAsync(string userId);
}