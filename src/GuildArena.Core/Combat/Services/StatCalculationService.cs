using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Stats;
using System.Collections.Generic;

namespace GuildArena.Core.Combat.Services;

public class StatCalculationService : IStatCalculationService
{    
    private readonly IReadOnlyDictionary<string, ModifierDefinition> _modifierDefinitions;
    
    public StatCalculationService(IModifierDefinitionRepository modifierRepository)
    {
        // Obtém o dicionário 1 vez e guarda-o.
        _modifierDefinitions = modifierRepository.GetAllDefinitions();
    }

    /// <summary>
    /// Gets the final calculated stat value for a combatant, 
    /// applying all active modifiers.
    /// </summary>
    public float GetStatValue(Combatant combatant, StatType statType)
    {        
        float baseValue = GetBaseStat(combatant.BaseStats, statType);
        float flatBonus = 0;
        float percentBonus = 0;

        // Iterar pelos Modifiers ATIVOS
        foreach (var activeMod in combatant.ActiveModifiers)
        {
            //  Procurar o Definition no dicionário
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef))
            {
                continue; // Modificador desconhecido
            }

            // Iterar pelas modificações de stats
            foreach (var statMod in modDef.StatModifications)
            {
                if (statMod.Stat == statType)
                {
                    // multiplicar pelo StackCount                     
                    float totalValue = statMod.Value * activeMod.StackCount;

                    if (statMod.Type == ModificationType.FLAT)
                    {
                        flatBonus += totalValue;
                    }
                    else if (statMod.Type == ModificationType.PERCENTAGE)
                    {
                        percentBonus += totalValue;
                    }
                }
            }
        }

        //  Aplicar os modifiers
        float finalValue = (baseValue + flatBonus) * (1 + percentBonus);

        //verificar valores abaixo de 0 (ou 0 no caso de maxHP)
        if (statType == StatType.MaxHP)
        {
            if (finalValue < 1) finalValue = 1;
        }
        else
        {
            if (finalValue < 0) finalValue = 0;
        }

        return (float)Math.Round(finalValue, MidpointRounding.AwayFromZero);
    }

    private float GetBaseStat(BaseStats stats, StatType statType)
    {
        return statType switch
        {
            StatType.Attack => stats.Attack,
            StatType.Defense => stats.Defense,
            StatType.Agility => stats.Agility,
            StatType.Magic => stats.Magic,
            StatType.MagicDefense => stats.MagicDefense,
            StatType.MaxActions => stats.MaxActions,
            StatType.MaxHP => stats.MaxHP,
            _ => 0f
        };
    }
}