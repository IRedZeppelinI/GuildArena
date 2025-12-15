using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Core.Combat.Services;

/// <inheritdoc />
public class CostCalculationService : ICostCalculationService
{
    private readonly IModifierDefinitionRepository _modifierRepo;

    public CostCalculationService(IModifierDefinitionRepository modifierRepo)
    {
        _modifierRepo = modifierRepo;
    }

    /// <inheritdoc />
    public FinalAbilityCosts CalculateFinalCosts(
        CombatPlayer caster,
        AbilityDefinition ability,
        List<Combatant> resolvedTargets,
        AbilityTargets userSelections) 
    {
        var definitions = _modifierRepo.GetAllDefinitions();

        // Copiar custos base
        var finalEssenceCosts = ability.Costs
            .Select(c => new EssenceAmount { Type = c.Type, Amount = c.Amount })
            .ToList();

        int finalHPCost = ability.HPCost;

        // 1. MODIFIERS DO CASTER (Descontos/Taxas próprias)        
        foreach (var mod in caster.ActiveModifiers)
        {
            if (!definitions.TryGetValue(mod.DefinitionId, out var def)) continue;

            foreach (var costMod in def.EssenceCostModifications)
            {
                if (costMod.RequiredAbilityTag != null && !ability.Tags.Contains(costMod.RequiredAbilityTag))
                    continue;

                ApplyEssenceModification(finalEssenceCosts, costMod);
            }

            foreach (var hpMod in def.HPCostModifications)
            {
                if (hpMod.RequiredAbilityTag != null && !ability.Tags.Contains(hpMod.RequiredAbilityTag))
                    continue;

                finalHPCost += hpMod.Value;
            }
        }

        // 2. WARDS DOS ALVOS (Targeting Tax)
        foreach (var target in resolvedTargets)
        {
            // Ward só funciona contra oponentes
            if (target.OwnerId == caster.PlayerId) continue;

            //Apenas aplicável a manual target, AoE ignora wards
            if (!IsManuallyTargeted(target.Id, userSelections))
            {
                continue;
            }           

            foreach (var mod in target.ActiveModifiers)
            {
                if (!definitions.TryGetValue(mod.DefinitionId, out var def)) continue;

                // Essence Ward 
                foreach (var wardCost in def.TargetingEssenceCosts)
                {
                    AddEssenceCost(finalEssenceCosts, wardCost);
                }

                // HP Ward 
                if (def.TargetingHPCost > 0)
                {
                    finalHPCost += def.TargetingHPCost;
                }
            }
        }

        if (finalHPCost < 0) finalHPCost = 0;

        return new FinalAbilityCosts
        {
            EssenceCosts = finalEssenceCosts.Where(c => c.Amount > 0).ToList(),
            HPCost = finalHPCost
        };
    }

    // --- Helper para verificar se foi alvo manual ---
    private bool IsManuallyTargeted(int targetId, AbilityTargets selections)
    {        
        foreach (var list in selections.SelectedTargets.Values)
        {
            if (list.Contains(targetId)) return true;
        }
        return false;
    }

    
    private void ApplyEssenceModification(List<EssenceAmount> costs, CostModification mod)
    {
        var targetType = mod.TargetEssenceType ?? EssenceType.Neutral;
        var existingCost = costs.FirstOrDefault(c => c.Type == targetType);

        if (existingCost != null)
        {
            existingCost.Amount += mod.Value;
        }
        else if (mod.Value > 0)
        {
            costs.Add(new EssenceAmount { Type = targetType, Amount = mod.Value });
        }
    }

    private void AddEssenceCost(List<EssenceAmount> costs, EssenceAmount extraCost)
    {
        var existing = costs.FirstOrDefault(c => c.Type == extraCost.Type);
        if (existing != null)
        {
            existing.Amount += extraCost.Amount;
        }
        else
        {
            costs.Add(new EssenceAmount { Type = extraCost.Type, Amount = extraCost.Amount });
        }
    }
}