using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.Combat.AI;

/// <summary>
/// Defines the strategy/logic used by the computer to play its turn.
/// </summary>
public interface IAiBehavior
{
    /// <summary>
    /// Analyzes the current game state and determines the next best valid action.
    /// </summary>
    /// <param name="gameState">The current snapshot of the combat.</param>
    /// <param name="aiPlayerId">The Player ID that the AI is controlling.</param>
    /// <returns>An intent to execute an ability, or null if no valid moves remain (end turn).</returns>
    AiActionIntent? DecideNextAction(GameState gameState, int aiPlayerId);
}