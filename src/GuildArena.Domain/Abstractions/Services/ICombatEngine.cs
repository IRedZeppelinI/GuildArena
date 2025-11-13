using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Domain.Abstractions.Services;

public interface ICombatEngine
{
    // Nota: Esta assinatura irá crescer para aceitar um GameState completo,
    // mas para o slice vertical do "ataque básico", isto é suficiente.
    void ExecuteAbility(AbilityDefinition ability, Combatant source, Combatant target);
}
