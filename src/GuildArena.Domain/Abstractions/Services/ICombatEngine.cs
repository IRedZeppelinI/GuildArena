using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Abstractions.Services;

public interface ICombatEngine
{    
    void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets 
    );
}
