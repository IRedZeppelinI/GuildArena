using GuildArena.Core.Combat.Actions; 
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Abstractions
{
    public interface IEffectHandler
    {
        EffectType SupportedType { get; }

        /// <summary>
        /// Applies the specific effect logic to the target and logs the outcome.
        /// </summary>
        /// <param name="effectDef">The definition of the effect to apply.</param>
        /// <param name="source">The combatant originating the effect.</param>
        /// <param name="target">The combatant receiving the effect.</param>
        /// <param name="gameState">The current state of the combat world.</param>
        /// <param name="actionResult">The result object to append Battle Logs to.</param>
        void Apply(
            EffectDefinition effectDef,
            Combatant source,
            Combatant target,
            GameState gameState,
            CombatActionResult actionResult);
    }
}