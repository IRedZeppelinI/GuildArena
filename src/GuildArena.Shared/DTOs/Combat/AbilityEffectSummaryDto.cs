using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Stats;

namespace GuildArena.Shared.DTOs.Combat;

public class AbilityEffectSummaryDto
{
    public EffectType Type { get; set; }

    // Para Dano e Cura (Matemática Pura)
    public int PredictedValue { get; set; } // O Dano/Cura final calculado para a Tooltip
    public float BaseAmount { get; set; }
    public StatType ScalingStat { get; set; }
    public float ScalingFactor { get; set; }

    // Para Aplicação de Modificadores (A UI precisa do texto para não mostrar apenas o ID "MOD_GUARD")
    public string? ModifierName { get; set; }
    public string? ModifierDescription { get; set; }
}
