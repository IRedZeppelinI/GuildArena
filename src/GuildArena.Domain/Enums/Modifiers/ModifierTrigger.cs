namespace GuildArena.Domain.Enums.Modifiers;

public enum ModifierTrigger
{
    PASSIVE,
    ON_TURN_START,
    ON_TURN_END,
    ON_DEAL_MELEE_ATTACK,
    ON_RECEIVE_MELEE_ATTACK,
    ON_DEAL_RANGED_ATTACK,
    ON_RECEIVE_RANGED_ATTACK,
    ON_DEAL_SPELL_ATTACK,
    ON_RECEIVE_SPELL_ATTACK,
    ON_DEAL_DAMAGE,
    ON_RECEIVE_DAMAGE,
    ON_DEAL_PHYSICAL_DAMAGE,
    ON_RECEIVE_PHYSICAL_DAMAGE,
    ON_DEAL_MAGIC_DAMAGE,
    ON_RECEIVE_MAGIC_DAMAGE,       
    ON_DEATH,

    /// <summary>
    /// Fires immediately after paying costs for an ability, but before effects are resolved.
    /// Useful for "On Cast" effects like "Gain 10 Rage when using a Skill".
    /// </summary>
    ON_ABILITY_CAST
}
