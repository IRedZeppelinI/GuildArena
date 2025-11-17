using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Implements the calculation logic for an ability's final cooldown,
/// applying passive buffs/debuffs from the combatant.
/// </summary>
public class CooldownCalculationService : ICooldownCalculationService
{
    private readonly IReadOnlyDictionary<string, ModifierDefinition> _modifierDefinitions;
    private readonly ILogger<CooldownCalculationService> _logger;

    public CooldownCalculationService(
        IModifierDefinitionRepository modifierRepository,
        ILogger<CooldownCalculationService> logger)
    {
        _modifierDefinitions = modifierRepository.GetAllDefinitions(); 
        _logger = logger;
    }

    /// <summary>
    /// Calculates the final cooldown of an ability, applying
    /// all active modifiers from the combatant.
    /// </summary>
    public int GetFinalCooldown(Combatant combatant, AbilityDefinition ability)
    {
        float baseCooldown = ability.BaseCooldown; //
        float flatBonus = 0;
        float percentBonus = 0;

        // 1. Iterar pelos Modifiers ATIVOS no combatente
        foreach (var activeMod in combatant.ActiveModifiers) 
        {
            // 2. Procurar o "molde" (Definition) no dicionário
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef))
            {
                continue; // Modificador desconhecido
            }

            // 3. Iterar pelas modificações de cooldown desse modifier
            foreach (var cdMod in modDef.CooldownModifications) 
            {
                // 4. Verificar se a regra de Tag bate certo
                bool tagMatch = cdMod.RequiredTag == null ||
                                ability.Tags.Contains(cdMod.RequiredTag); 

                if (tagMatch)
                {
                    if (cdMod.Type == ModificationType.FLAT) 
                    {
                        flatBonus += cdMod.Value;
                    }
                    else if (cdMod.Type == ModificationType.PERCENTAGE) 
                    {
                        percentBonus += cdMod.Value;
                    }
                }
            }
        }

        // 5. Aplicar a fórmula (idêntica à dos stats)
        float finalValue = (baseCooldown + flatBonus) * (1 + percentBonus);

        // Arredondar e garantir que nunca é negativo
        int finalIntCooldown = (int)Math.Round(finalValue, MidpointRounding.AwayFromZero);

        return Math.Max(0, finalIntCooldown);
    }
}