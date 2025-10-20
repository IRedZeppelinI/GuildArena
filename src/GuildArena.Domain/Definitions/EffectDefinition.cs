using GuildArena.Domain.Enums;

namespace GuildArena.Domain.Definitions;

public class EffectDefinition
{
    public EffectType Type { get; set; } // Enum: DAMAGE
    public float BaseAmount { get; set; }
    public StatType ScalingStat { get; set; }
    public float ScalingFactor { get; set; }

    public string ModifierDefinitionId { get; set; } // O ID do buff/debuff (ex: "MOD_POISON_WEAK")
    public int DurationInTurns { get; set; } // Quantos turnos? (-1 para permanente)
}
