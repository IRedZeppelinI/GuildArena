using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Extensions; 

namespace GuildArena.Core.Combat.Services;

public class StatusConditionService : IStatusConditionService
{
    public ActionStatusResult CheckStatusConditions(Combatant source, AbilityDefinition ability)
    {
        // Itera sobre todos os modificadores ativos para verificar os estados
        foreach (var mod in source.ActiveModifiers)
        {
            foreach (var status in mod.ActiveStatusEffects)
            {
                // 1. Verifica Hard CC (Stun)
                if (status.BlocksAllActions())
                {
                    return ActionStatusResult.Stunned;
                }

                // Determinar se é ataque básico ou skill
                // (Assume-se que BasicAttackId está preenchido corretamente no Combatant)
                bool isBasicAttack = source.BasicAttack != null && source.BasicAttack.Id == ability.Id;

                if (isBasicAttack)
                {
                    // 2. Verifica Bloqueio de Ataques (Disarm)
                    if (status.BlocksBasicAttack())
                    {
                        return ActionStatusResult.Disarmed;
                    }
                }
                else
                {
                    // 3. Verifica Bloqueio de Skills (Silence)
                    if (status.BlocksSkills())
                    {
                        return ActionStatusResult.Silenced;
                    }
                }
            }
        }

        return ActionStatusResult.Allowed;
    }
}