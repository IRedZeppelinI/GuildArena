using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories; // <-- Adicionar
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using System.Collections.Generic;

namespace GuildArena.Core.Combat.Services;

public class StatCalculationService : IStatCalculationService
{
    // "repo"  BD de JSONs
    private readonly IReadOnlyDictionary<string, ModifierDefinition> _modifierDefinitions;

    // repositório via DI
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

        // 2. Iterar pelos Modifiers ATIVOS
        foreach (var activeMod in combatant.ActiveModifiers)
        {
            // 3. Procurar o "molde" (Definition) no dicionário
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef))
            {
                continue; // Modificador desconhecido
            }

            // 4. Iterar pelas modificações de stats
            foreach (var statMod in modDef.StatModifications)
            {
                if (statMod.Stat == statType)
                {
                    if (statMod.Type == ModificationType.FLAT)
                    {
                        flatBonus += statMod.Value;
                    }
                    else if (statMod.Type == ModificationType.PERCENTAGE)
                    {
                        percentBonus += statMod.Value;
                    }
                }
            }
        }

        //  Aplicar os modifiers
        float finalValue = (baseValue + flatBonus) * (1 + percentBonus);

        return (float)Math.Round(finalValue);
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
            _ => 0f
        };
    }
}