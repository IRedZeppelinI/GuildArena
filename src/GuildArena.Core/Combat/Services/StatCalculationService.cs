using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Services;

public class StatCalculationService : IStatCalculationService
{

    /// <summary>
    /// Gets the final calculated stat value for a combatant.
    /// (Prototype: This currently only reads pre-calculated stats).
    /// </summary>
    public float GetStatValue(Combatant combatant, StatType statType)
    {
        // NOTA: Para o protótipo, lemos "CalculatedStats".
        // No futuro, esta função irá *calcular* os stats totais 
        // (Base + Nível + Equipamento + Modifiers) em tempo real.

        return statType switch
        {
            StatType.Attack => combatant.CalculatedStats.Attack,
            StatType.Defense => combatant.CalculatedStats.Defense,
            StatType.Agility => combatant.CalculatedStats.Agility,
            StatType.Magic => combatant.CalculatedStats.Magic,
            _ => 0f
        };
    }
}