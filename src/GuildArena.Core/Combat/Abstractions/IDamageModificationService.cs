using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service responsible for calculating damage/healing modifications
/// based on the active modifiers of the source and target.
/// </summary>
public interface IDamageModificationService
{
    /// <summary>
    /// Calculates the final damage/heal value after applying all tag-based modifiers.
    /// </summary>
    /// <param name="baseDamage">The initial damage/heal (after stats and defense).</param>
    /// <param name="effect">The effect being applied (to read its Tags).</param>
    /// <param name="source">The combatant applying the effect.</param>
    /// <param name="target">The combatant receiving the effect.</param>
    /// <returns>The final modified damage/heal value.</returns>
    float CalculateModifiedValue(float baseDamage, EffectDefinition effect, Combatant source, Combatant target);
}