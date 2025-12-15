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
    public ModifierType Type { get; set; } // Enum: BUFF, DEBUFF

    
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



    public List<ModifierTrigger> Triggers { get; set; } = new(); // Enum: ON_TURN_START, ON_TAKE_DAMAGE...       
    public string? TriggeredAbilityId { get; set; } // Ex: "INTERNAL_POISON_TICK"
}


