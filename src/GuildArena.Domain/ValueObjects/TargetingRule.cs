using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects;

public class TargetingRule
{
    // O ID que o EffectDefinition vai referenciar
    public required string RuleId { get; set; } 
    public TargetType Type { get; set; }
    public int Count { get; set; } // 1 para single-target - 99 para AoE
    public bool CanTargetDead { get; set; } = false;

    public TargetSelectionStrategy Strategy { get; set; } = TargetSelectionStrategy.Manual;
}