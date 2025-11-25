using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Abstractions
{
    public interface IEffectHandler
    {
        EffectType SupportedType { get; } // Ex: DAMAGE

        /// <summary>
        /// Applies the specific effect logic to the target.
        /// </summary>
        /// <param name="effectDef">The definition of the effect to apply.</param>
        /// <param name="source">The combatant originating the effect.</param>
        /// <param name="target">The combatant receiving the effect.</param>
        /// <param name="gameState">The current state of the combat world (required for triggers and context).</param>
        void Apply(EffectDefinition effectDef, Combatant source, Combatant target, GameState gameState);
    }
}
