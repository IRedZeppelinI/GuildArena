using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Abstractions
{
    public interface IEffectHandler
    {
        EffectType SupportedType { get; } // Ex: DAMAGE
        void Apply(EffectDefinition effectDef, Combatant source, Combatant target);
    }
}
