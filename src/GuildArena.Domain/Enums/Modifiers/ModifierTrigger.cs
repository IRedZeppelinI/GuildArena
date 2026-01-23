namespace GuildArena.Domain.Enums.Modifiers;

public enum ModifierTrigger
{
    PASSIVE,
    /// <summary>
    /// Fires once at the very beginning of the combat, before the first turn.
    /// Used for racial traits (e.g. Start with Flux) or items.
    /// </summary>
    ON_COMBAT_START,
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
    ON_DEAL_HEAL,      
    ON_RECEIVE_HEAL,   

    /// <summary>
    /// Fires immediately after paying costs for an ability, but before effects are resolved.
    /// Useful for "On Cast" effects like "Gain 10 Rage when using a Skill".
    /// </summary>
    ON_ABILITY_CAST,

    /// <summary>
    /// Fires when a combatant successfully evades an incoming hostile effect.
    /// </summary>
    ON_EVADE,

    ON_DEATH,
}
