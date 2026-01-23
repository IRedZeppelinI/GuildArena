using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.ValueObjects.Resources;

namespace GuildArena.Domain.Definitions;

public class EffectDefinition
{
    public EffectType Type { get; set; } // Enum: DAMAGE
    public DamageCategory DamageCategory { get; set; }
    public DeliveryMethod Delivery { get; set; }
    public float BaseAmount { get; set; }
    public StatType ScalingStat { get; set; }
    public float ScalingFactor { get; set; }    

    public List<string> Tags { get; set; } = new();

    public required string TargetRuleId { get; set; }

    public string? ModifierDefinitionId { get; set; } // O ID do buff/debuff
    public int DurationInTurns { get; set; } // (-1 para permanente)

    /// <summary>
    /// Indicates if this effect can be evaded (dodged/missed) by the target.
    /// If false, the effect is guaranteed to hit (e.g., buffs, reactive triggers).
    /// Default is true.
    /// </summary>
    public bool CanBeEvaded { get; set; } = true;

    /// <summary>
    /// Percentage of Target's Defense/MagicDefense to ignore.
    /// Range: 0.0 to 1.0. Default 0.
    /// </summary>
    public float DefensePenetration { get; set; } = 0f;

    /// <summary>
    /// The status effect required on the target to trigger conditional bonuses (Crit/Hit).
    /// </summary>
    public StatusEffectType? ConditionStatus { get; set; }

    /// <summary>
    /// Multiplier applied to damage if ConditionStatus is met.
    /// Default 1.0 (No change).
    /// </summary>
    public float ConditionDamageMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// If true and ConditionStatus is met, the attack cannot be evaded.
    /// </summary>
    public bool ConditionGuaranteedHit { get; set; } = false;


    /// <summary>
    /// Defines the list of essence manipulations to execute immediately (Add/Remove).
    /// Used only when EffectType is MANIPULATE_ESSENCE.
    /// </summary>
    public List<EssenceAmount> EssenceManipulations { get; set; } = new();
}
