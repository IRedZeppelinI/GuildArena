using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.ValueObjects.Modifiers;
using GuildArena.Domain.ValueObjects.Resources;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines the static blueprint for a modifier (buff/debuff).
/// </summary>
public class ModifierDefinition
{
    public required string Id { get; set; } // "MOD_POISON_WEAK"
    public required string Name { get; set; } // "Veneno Fraco"
    public ModifierType Type { get; set; } // Enum: Bless, Curse

    /// <summary>
    /// The maximum number of times this modifier can stack.
    /// Default is 1 (does not stack, just refreshes).
    /// </summary>
    public int MaxStacks { get; set; } = 1;


    public List<StatModification> StatModifications { get; set; } = new(); // Ex: { Attack: 10, Defense: -5, ... }

    public List<DamageModification> DamageModifications { get; set; } = new();
    public List<CooldownModification> CooldownModifications { get; set; } = new();


    /// <summary>
    /// Modifications that affect ability essence costs.
    /// </summary>
    public List<CostModification> EssenceCostModifications { get; set; } = new();


    /// <summary>
    /// Modifications that affect ability HP costs.
    /// </summary>
    public List<HPCostModification> HPCostModifications { get; set; } = new();


    /// <summary>
    /// Modifications that affect turn-start essence generation.
    /// </summary>
    public List<EssenceGenerationModification> EssenceGenerationModifications { get; set; } = new();


    /// <summary>
    /// Additional costs that an opponent must pay to target the unit 
    /// possessing this modifier (Ward mechanic).
    /// </summary>
    public List<EssenceAmount> TargetingEssenceCosts { get; set; } = new();


    /// <summary>
    /// Additional HP cost that an opponent must pay (damage taken) to target the unit 
    /// possessing this modifier (Blood Ward mechanic).
    /// </summary>
    public int TargetingHPCost { get; set; }


    /// <summary>
    /// The list of status effects granted by this modifier while active.
    /// Replaces individual booleans like IsStunned, IsInvulnerable.
    /// </summary>
    public List<StatusEffectType> GrantedStatusEffects { get; set; } = new();


    /// <summary>
    /// Defines the barrier properties granted by this modifier, if any.
    /// </summary>
    public BarrierProperties? Barrier { get; set; }

    /// <summary>
    /// Modifications that affect the strength of barriers created by this character.
    /// </summary>
    public List<BarrierModification> BarrierModifications { get; set; } = new();

    /// <summary>
    /// If true, this modifier is automatically removed from the target 
    /// if the caster (originator) dies.
    /// Useful for channeled spells, links, or taunts.
    /// Default: false.
    /// </summary>
    public bool RemoveOnCasterDeath { get; set; } = false;

    /// <summary>
    /// If true, defensive/reactive triggers (e.g. ON_RECEIVE_HEAL) will fire even if the event 
    /// happened to an ally, not the modifier holder.
    /// Default is false.
    /// </summary>
    public bool TriggerOnAllies { get; set; } = false;

    /// <summary>
    /// If true, defensive/reactive triggers will fire even if the event happened to an enemy.
    /// Default is false.
    /// </summary>
    public bool TriggerOnEnemies { get; set; } = false;

    /// <summary>
    /// If true, the modifier is automatically removed from the holder immediately after 
    /// any of its triggers fires successfully. 
    /// Useful for "Next Attack deals X" or "Start of Combat" one-time effects.
    /// Default is false.
    /// </summary>
    public bool RemoveAfterTrigger { get; set; } = false;

    public List<ModifierTrigger> Triggers { get; set; } = new(); // Enum: ON_TURN_START, ON_TAKE_DAMAGE...       
    public string? TriggeredAbilityId { get; set; }
}


