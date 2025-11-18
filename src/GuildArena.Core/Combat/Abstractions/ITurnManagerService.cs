using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a specialist service for managing the combat turn flow.
/// </summary>
public interface ITurnManagerService
{
    /// <summary>
    /// Advances the combat state to the next player's turn,
    /// applying all end-of-turn and start-of-turn effects.
    /// </summary>
    /// <param name="gameState">The current state of the combat to be modified.</param>
    void AdvanceTurn(GameState gameState);
}