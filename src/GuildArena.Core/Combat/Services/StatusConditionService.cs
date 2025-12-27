using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;

namespace GuildArena.Core.Combat.Services;

public class StatusConditionService : IStatusConditionService
{
    public ActionStatusResult CheckStatusConditions(Combatant source, AbilityDefinition ability)
    {
        foreach (var mod in source.ActiveModifiers)
        {
            foreach (var status in mod.ActiveStatusEffects)
            {
                // 1. Hard CC (Stun)
                // Bloqueia qualquer ação, independentemente de tags.
                if (status == StatusEffectType.Stun)
                {
                    return ActionStatusResult.Stunned;
                }

                // 2. Disarm (Bloqueia Armas/Físico)                
                if (status == StatusEffectType.Disarm)
                {
                    if (ability.Tags.Contains("Melee", StringComparer.OrdinalIgnoreCase) ||
                        ability.Tags.Contains("Ranged", StringComparer.OrdinalIgnoreCase) ||
                        ability.Tags.Contains("Weapon", StringComparer.OrdinalIgnoreCase) ||
                        ability.Tags.Contains("Physical", StringComparer.OrdinalIgnoreCase))
                    {
                        return ActionStatusResult.Disarmed;
                    }
                }

                // 3. Silence (Bloqueia Magia)                
                if (status == StatusEffectType.Silence)
                {
                    if (ability.Tags.Contains("Spell", StringComparer.OrdinalIgnoreCase) ||
                        ability.Tags.Contains("Magic", StringComparer.OrdinalIgnoreCase) ||
                        ability.Tags.Contains("Elemental", StringComparer.OrdinalIgnoreCase))
                    {
                        return ActionStatusResult.Silenced;
                    }
                }
            }
        }

        return ActionStatusResult.Allowed;
    }
}