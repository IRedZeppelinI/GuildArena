using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service for calculating the final value of a combatant's stat.
/// </summary>
public interface IStatCalculationService
{
    /// <summary>
    /// Gets the final calculated stat value for a combatant.
    /// (This will eventually grow to include buffs, equipment, and level scaling).
    /// </summary>
    float GetStatValue(Combatant combatant, StatType statType);
}
