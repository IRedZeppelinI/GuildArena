using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

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
    public List<EssenceCost> TargetingEssenceCosts { get; set; } = new();


    /// <summary>
    /// Additional HP cost that an opponent must pay (damage taken) to target the unit 
    /// possessing this modifier (Blood Ward mechanic).
    /// </summary>
    public int TargetingHPCost { get; set; }


    /// <summary>
    /// If true, the unit cannot be manually targeted by opponents while this modifier is active 
    /// (e.g., Stealth, Camouflage). Area effects (AoE) may still hit depending on rule logic.
    /// </summary>
    public bool IsUntargetable { get; set; }

    /// <summary>
    /// If true, the unit is immune to ALL negative effects and damage while this modifier is active.
    /// (e.g., Ice Block, Divine Shield).
    /// </summary>
    public bool IsInvulnerable { get; set; }



    public List<ModifierTrigger> Triggers { get; set; } = new(); // Enum: ON_TURN_START, ON_TAKE_DAMAGE...       
    public string? TriggeredAbilityId { get; set; } // Ex: "INTERNAL_POISON_TICK"
}


