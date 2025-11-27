using GuildArena.Domain.Enums;

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
}
