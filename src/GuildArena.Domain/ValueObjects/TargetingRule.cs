using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects;

public class TargetingRule
{
    // O ID que o EffectDefinition vai referenciar
    public required string RuleId { get; set; } // Ex: "T_Enemy", "T_Ally"
    public TargetType Type { get; set; }
    public int Count { get; set; } // Quantos? (1 para single-target, 99 para AoE)
}