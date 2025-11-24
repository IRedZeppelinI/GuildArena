using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a unified service for calculating final damage, applying modifiers, resistances, and barrier absorption.
/// </summary>
public interface IDamageResolutionService
{
    /// <summary>
    /// Resolves the raw damage by applying modifiers (buffs/resists) and processing barrier absorption.
    /// </summary>
    DamageResolutionResult ResolveDamage(
        float baseDamage,
        EffectDefinition effect,
        Combatant source,
        Combatant target);
}