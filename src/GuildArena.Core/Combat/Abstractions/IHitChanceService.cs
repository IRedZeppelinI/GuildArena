using GuildArena.Domain.Definitions;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service responsible for calculating the probability of an effect hitting its target.
/// </summary>
public interface IHitChanceService
{
    /// <summary>
    /// Calculates the hit chance (0.0 to 1.0) based on source and target stats.
    /// </summary>
    float CalculateHitChance(Combatant source, Combatant target, EffectDefinition effect);
}