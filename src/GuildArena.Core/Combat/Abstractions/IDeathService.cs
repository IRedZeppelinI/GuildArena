using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Orchestrates the transition of a combatant from Alive to Dead.
/// Handles logging, triggers (ON_DEATH), external cleanup (links), and internal cleanup (buff removal).
/// </summary>
public interface IDeathService
{
    /// <summary>
    /// Checks if the victim is dead (HP less than or equal to 0). 
    /// If so, executes the death processing pipeline.
    /// </summary>
    /// <param name="victim">The combatant who took damage or paid a cost.</param>
    /// <param name="killer">The combatant responsible for the death (can be the victim itself).</param>
    /// <param name="gameState">The current combat state.</param>
    void ProcessDeathIfApplicable(Combatant victim, Combatant killer, GameState gameState);
}