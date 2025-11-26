using GuildArena.Core.Combat.Enums;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service responsible for validating if a combatant's current status effects
/// allow them to perform a specific ability.
/// </summary>
public interface IStatusConditionService
{
    /// <summary>
    /// Checks if the combatant has any active status effect that prevents the specific ability.
    /// </summary>
    ActionStatusResult CheckStatusConditions(Combatant source, AbilityDefinition ability);
}