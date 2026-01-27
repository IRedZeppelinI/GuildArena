using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Core.Combat.Actions;

/// <summary>
/// Defines the contract for an atomic action within the combat loop.
/// Encapsulates logic so it can be queued and executed sequentially.
/// </summary>
public interface ICombatAction
{
    /// <summary>
    /// Gets the descriptive name of the action (e.g., "Cast Fireball", "Apply Poison").
    /// Used for debugging and system logs.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The combatant responsible for initiating this action.
    /// </summary>
    Combatant Source { get; }

    /// <summary>
    /// Executes the action logic.
    /// </summary>
    /// <param name="engine">The combat engine instance (providing access to calculation services).</param>
    /// <param name="gameState">The current mutable state of the combat.</param>
    /// <returns>A result containing Battle Logs for the client and execution status.</returns>
    CombatActionResult Execute(ICombatEngine engine, GameState gameState);
}