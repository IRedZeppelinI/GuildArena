using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;

namespace GuildArena.Domain.ValueObjects.Modifiers;

// Isto descreve UMA modificação de stat (ex: +10 Attack)
public class StatModification
{
    public StatType Stat { get; set; } // O stat a modificar (ex: Attack)
    public ModificationType Type { get; set; } // FLAT ou PERCENTAGE
    public float Value { get; set; } // O valor (ex: 10 ou 0.05)
}