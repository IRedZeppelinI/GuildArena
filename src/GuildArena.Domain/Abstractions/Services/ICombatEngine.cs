using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Abstractions.Services;

public interface ICombatEngine
{

    /// <summary>
    /// Executes an ability, validating costs, cooldowns, and applying effects.
    /// </summary>
    /// <param name="currentState">The current state of the combat.</param>
    /// <param name="ability">The definition of the ability being used.</param>
    /// <param name="source">The combatant using the ability.</param>
    /// <param name="targets">The selected targets.</param>
    /// <param name="payment">The specific essence allocation provided by the player to pay for the ability.</param>
    void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> payment
    );
}
