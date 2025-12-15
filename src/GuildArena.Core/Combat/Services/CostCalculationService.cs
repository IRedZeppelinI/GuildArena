using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects.Modifiers;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <inheritdoc />
public class CostCalculationService : ICostCalculationService
{
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(
        IModifierDefinitionRepository modifierRepo,
        ILogger<CostCalculationService> logger)
    {
        _modifierRepo = modifierRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public FinalAbilityCosts CalculateFinalCosts(
        CombatPlayer caster,
        AbilityDefinition ability,
        List<Combatant> resolvedTargets,
        AbilityTargets userSelections)
    {
        var definitions = _modifierRepo.GetAllDefinitions();

        // 0. Copiar custos base da habilidade
        var finalEssenceCosts = ability.Costs
            .Select(c => new EssenceAmount { Type = c.Type, Amount = c.Amount })
            .ToList();

        int finalHPCost = ability.HPCost;

        // =========================================================
        // 1. MODIFIERS DO CASTER (Descontos, Penalidades, Raça)
        // =========================================================
        foreach (var mod in caster.ActiveModifiers)
        {
            if (!definitions.TryGetValue(mod.DefinitionId, out var def)) continue;

            // A. Essence Costs
            foreach (var costMod in def.EssenceCostModifications)
            {
                if (costMod.RequiredAbilityTag != null && !ability.Tags.Contains(costMod.RequiredAbilityTag))
                    continue;

                // Filtro por Raça
                if (!CheckRacialCondition(costMod.TargetRaceId, costMod.Value, resolvedTargets, userSelections))
                    continue;

                ApplyEssenceModification(finalEssenceCosts, costMod);

                // Log de Debug
                if (costMod.Value != 0)
                {
                    _logger.LogDebug(
                        "Caster Mod {ModName} applied essence modification: {Value} (TargetRace: {Race}).",
                        def.Name, costMod.Value, costMod.TargetRaceId ?? "None");
                }
            }

            // B. HP Costs
            foreach (var hpMod in def.HPCostModifications)
            {
                if (hpMod.RequiredAbilityTag != null && !ability.Tags.Contains(hpMod.RequiredAbilityTag))
                    continue;

                // Filtro por Raça
                if (!CheckRacialCondition(hpMod.TargetRaceId, hpMod.Value, resolvedTargets, userSelections))
                    continue;

                finalHPCost += hpMod.Value;

                // Log de Debug
                if (hpMod.Value != 0)
                {
                    _logger.LogDebug(
                        "Caster Mod {ModName} applied HP modification: {Value} (TargetRace: {Race}).",
                        def.Name, hpMod.Value, hpMod.TargetRaceId ?? "None");
                }
            }
        }

        // =========================================================
        // 2. WARDS DOS ALVOS 
        // =========================================================
        foreach (var target in resolvedTargets)
        {
            // Ward só funciona contra oponentes
            if (target.OwnerId == caster.PlayerId) continue;

            //  Ward só se aplica se o alvo foi selecionado MANUALMENTE.
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
                    _logger.LogDebug(
                        "Target {TargetName} applied Ward Tax (Essence): {Amount} {Type} from {ModName}.",
                        target.Name, wardCost.Amount, wardCost.Type, def.Name);
                }

                // HP Ward 
                if (def.TargetingHPCost > 0)
                {
                    finalHPCost += def.TargetingHPCost;
                    _logger.LogDebug(
                        "Target {TargetName} applied Blood Ward Tax: {Amount} HP from {ModName}.",
                        target.Name, def.TargetingHPCost, def.Name);
                }
            }
        }

        // Clamp final
        if (finalHPCost < 0) finalHPCost = 0;

        return new FinalAbilityCosts
        {
            EssenceCosts = finalEssenceCosts.Where(c => c.Amount > 0).ToList(),
            HPCost = finalHPCost
        };
    }

    // --- Helpers  ---

    private bool CheckRacialCondition(
        string? requiredRaceId,
        int modifierValue,
        List<Combatant> resolvedTargets,
        AbilityTargets userSelections)
    {
        if (string.IsNullOrEmpty(requiredRaceId)) return true;

        var manualTargets = resolvedTargets
            .Where(t => IsManuallyTargeted(t.Id, userSelections))
            .ToList();

        if (manualTargets.Count == 0) return false;

        // Penalidade (Value > 0): Basta um alvo ser da raça.
        // Desconto (Value < 0): Todos os alvos têm de ser da raça.
        return modifierValue > 0
            ? manualTargets.Any(t => t.RaceId == requiredRaceId)
            : manualTargets.All(t => t.RaceId == requiredRaceId);
    }

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