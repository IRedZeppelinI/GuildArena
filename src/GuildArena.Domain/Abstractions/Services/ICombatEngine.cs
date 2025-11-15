using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Domain.Abstractions.Services;

public interface ICombatEngine
{    
    void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        List<int> selectedTargetIds // Apenas os IDs que a UI enviou
    );
}
