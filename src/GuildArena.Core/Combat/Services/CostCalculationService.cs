using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Implements the logic for calculating final ability costs, covering Essence, HP, Modifiers, and Wards.
/// </summary>
public class CostCalculationService : ICostCalculationService
{
    private readonly IModifierDefinitionRepository _modifierRepo;

    public CostCalculationService(IModifierDefinitionRepository modifierRepo)
    {
        _modifierRepo = modifierRepo;
    }

    /// <summary>
    /// Calculates the final essence and HP costs (the "Invoice") to execute an ability against specific targets.
    /// </summary>
    public FinalAbilityCosts CalculateFinalCosts(
        CombatPlayer caster,
        AbilityDefinition ability,
        List<Combatant> targets)
    {
        var definitions = _modifierRepo.GetAllDefinitions();

        
        // Copiar custos base da habilidade para não alterar obj original da cache        
        var finalEssenceCosts = ability.Costs
            .Select(c => new EssenceCost { Type = c.Type, Amount = c.Amount })
            .ToList();

        int finalHPCost = ability.HPCost;

        //  MODIFIERS DO CASTER 
        foreach (var mod in caster.ActiveModifiers)
        {
            if (!definitions.TryGetValue(mod.DefinitionId, out var def)) continue;

            // essence Modifiers
            foreach (var costMod in def.EssenceCostModifications)
            {
                // Verifica mod por tag para essence
                if (costMod.RequiredAbilityTag != null && !ability.Tags.Contains(costMod.RequiredAbilityTag))
                    continue;

                ApplyEssenceModification(finalEssenceCosts, costMod);
            }

            // Verifica mod por tag para hpCost
            foreach (var hpMod in def.HPCostModifications)
            {
                if (hpMod.RequiredAbilityTag != null && !ability.Tags.Contains(hpMod.RequiredAbilityTag))
                    continue;

                finalHPCost += hpMod.Value;
            }
        }

        // WARDS DOS ALVOS 
        foreach (var target in targets)
        {
            
            // ward só funciona contra oponentes
            if (target.OwnerId == caster.PlayerId) continue;

            foreach (var mod in target.ActiveModifiers)
            {
                if (!definitions.TryGetValue(mod.DefinitionId, out var def)) continue;

                //  Essence Ward 
                foreach (var wardCost in def.TargetingEssenceCosts)
                {
                    AddEssenceCost(finalEssenceCosts, wardCost);
                }

                //  HP Ward 
                if (def.TargetingHPCost > 0)
                {
                    finalHPCost += def.TargetingHPCost;
                }
            }
        }

        
        // Garantir que não devolve custos negativos de HP
        if (finalHPCost < 0) finalHPCost = 0;

        return new FinalAbilityCosts
        {
            // Filtra essences a 0 ou negativas (caso descontos tenham eliminado o custo)
            EssenceCosts = finalEssenceCosts.Where(c => c.Amount > 0).ToList(),
            HPCost = finalHPCost
        };
    }

    //  Helpers 

    private void ApplyEssenceModification(List<EssenceCost> costs, CostModification mod)
    {
        // Se TargetEssenceType for null, assumimos que reduz o custo Neutral primeiro
        var targetType = mod.TargetEssenceType ?? EssenceType.Neutral;

        var existingCost = costs.FirstOrDefault(c => c.Type == targetType);

        if (existingCost != null)
        {
            existingCost.Amount += mod.Value;
        }
        else if (mod.Value > 0) // Só adiciona se for debuff (aumento de custo)
        {
            costs.Add(new EssenceCost { Type = targetType, Amount = mod.Value });
        }
    }

    private void AddEssenceCost(List<EssenceCost> costs, EssenceCost extraCost)
    {
        var existing = costs.FirstOrDefault(c => c.Type == extraCost.Type);
        if (existing != null)
        {
            existing.Amount += extraCost.Amount;
        }
        else
        {
            costs.Add(new EssenceCost { Type = extraCost.Type, Amount = extraCost.Amount });
        }
    }
}