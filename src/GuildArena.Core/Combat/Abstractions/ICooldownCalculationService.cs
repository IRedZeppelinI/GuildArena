using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a specialist service for calculating the final cooldown of an ability,
/// applying all active modifiers from the combatant.
/// </summary>
public interface ICooldownCalculationService
{
    /// <summary>
    /// Calculates the final cooldown of an ability, applying
    /// all active modifiers from the combatant.
    /// </summary>
    /// <returns>The final number of cooldown turns (minimum 0).</returns>
    int GetFinalCooldown(Combatant combatant, AbilityDefinition ability);
}