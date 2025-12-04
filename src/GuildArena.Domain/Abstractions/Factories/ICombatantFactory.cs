using GuildArena.Domain.Entities;

namespace GuildArena.Domain.Abstractions.Factories;

/// <summary>
/// Defines the contract for creating a battle-ready Combatant from persistent hero data.
/// Handles stat scaling, racial bonuses, and ability setup.
/// </summary>
public interface ICombatantFactory
{
    /// <summary>
    /// Creates a fully initialized Combatant instance based on the hero's persistent data.
    /// </summary>
    /// <param name="hero">The persistent hero data (Level, IDs, etc).</param>
    /// <param name="ownerId">The Player ID controlling this combatant.</param>
    /// <returns>A combatant with calculated stats and active modifiers.</returns>
    Combatant Create(HeroCharacter hero, int ownerId);
}