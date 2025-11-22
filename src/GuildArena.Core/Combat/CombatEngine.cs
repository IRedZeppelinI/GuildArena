using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat;

/// <summary>
/// The main orchestrator for combat actions. Responsible for validating rules, 
/// processing costs, managing cooldowns, and executing ability effects.
/// </summary>
public class CombatEngine : ICombatEngine
{
    
    private readonly IReadOnlyDictionary<EffectType, IEffectHandler> _handlers;
    private readonly ILogger<CombatEngine> _logger;
    private readonly ICooldownCalculationService _cooldownCalcService;
    private readonly ICostCalculationService _costCalcService;
    private readonly IEssenceService _essenceService;
    private readonly ITargetResolutionService _targetService;
    private readonly IModifierDefinitionRepository _modifierRepo;

    public CombatEngine(
        IEnumerable<IEffectHandler> handlers,
        ILogger<CombatEngine> logger,
        ICooldownCalculationService cooldownCalcService,
        ICostCalculationService costCalcService, 
        IEssenceService essenceService,
        ITargetResolutionService targetService, 
        IModifierDefinitionRepository modifierRepo)
    {
        // O construtor (via DI) recebe *todos* os handlers e organiza-os
        // num dicionário para acesso instantâneo.
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
        _logger = logger;
        _cooldownCalcService = cooldownCalcService;
        _costCalcService = costCalcService;
        _essenceService = essenceService;
        _targetService = targetService;
        _modifierRepo = modifierRepo;
    }

    /// <inheritdoc />
    public void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> resourceAllocation)
    {
        // RESOLVER ALVOS
        var resolvedTargets = new List<Combatant>();
        foreach (var rule in ability.TargetingRules)
        {
            var ruleTargets = _targetService.ResolveTargets(rule, source, currentState, targets);
            resolvedTargets.AddRange(ruleTargets);
        }

        // VERIFICAR PRÉ-CONDIÇÕES
        if (!CanExecuteAbility(source, currentState, ability, resolvedTargets, resourceAllocation, out var calculatedCost))
        {
            return;
        }

        _logger.LogInformation("Executing ability {AbilityId} from {SourceId}", ability.Id, source.Id);

        //  PAGAR CUSTOS
        ConsumeAbilityResources(source, currentState, resourceAllocation, calculatedCost);

        //  APLICAR COOLDOWN
        ApplyAbilityCooldown(source, ability);

        // RESOLVER EFEITOS
        foreach (var effect in ability.Effects)
        {
            if (!_handlers.TryGetValue(effect.Type, out var handler)) continue;

            var rule = ability.TargetingRules.FirstOrDefault(r => r.RuleId == effect.TargetRuleId);
            if (rule == null) continue;

            // Re-resolver targets para o efeito específico para garantir consistência
            var effectTargets = _targetService.ResolveTargets(rule, source, currentState, targets);

            foreach (var target in effectTargets)
            {
                //  VERIFICAÇÃO IMUNIDADE
                if (IsCombatantInvulnerable(target))
                {
                    _logger.LogInformation("{Target} is invulnerable. Effect {Effect} ignored.", target.Name, effect.Type);
                    continue; // O efeito naõ resolve e não acontece nada
                }

                handler.Apply(effect, source, target);
            }
        }
    }

    // Helper verificar Invulnerable
    private bool IsCombatantInvulnerable(Combatant target)
    {
        var defs = _modifierRepo.GetAllDefinitions();
        foreach (var mod in target.ActiveModifiers)
        {
            if (defs.TryGetValue(mod.DefinitionId, out var def))
            {
                if (def.IsInvulnerable) return true;
            }
        }
        return false;
    }

    //Helpers
    /// <summary>
    /// Validates if the ability can be executed by checking cooldowns, calculating costs, 
    /// and verifying if the player possesses enough resources (HP and Essence).
    /// </summary>
    private bool CanExecuteAbility(
        Combatant source,
        GameState state,
        AbilityDefinition ability,
        List<Combatant> targets,
        Dictionary<EssenceType, int> payment,
        out FinalAbilityCosts calculatedCost)
    {
        calculatedCost = null!;

        // Cooldowns
        var existingCooldown = source.ActiveCooldowns.FirstOrDefault(c => c.AbilityId == ability.Id);
        if (existingCooldown != null)
        {
            _logger.LogWarning("Ability {Id} on cooldown ({Turns} turns).", ability.Id, existingCooldown.TurnsRemaining);
            return false;
        }

        // Calcular Custos 
        var casterPlayer = state.Players.First(p => p.PlayerId == source.OwnerId);
        calculatedCost = _costCalcService.CalculateFinalCosts(casterPlayer, ability, targets);

        // Validar HP 
        if (calculatedCost.HPCost > 0 && source.CurrentHP <= calculatedCost.HPCost)
        {
            _logger.LogWarning("Not enough HP to pay cost. HP: {HP}, Cost: {Cost}", source.CurrentHP, calculatedCost.HPCost);
            return false;
        }

        //  Validar Essence         
        if (!_essenceService.HasEnoughEssence(casterPlayer, calculatedCost.EssenceCosts))
        {
            _logger.LogWarning("Player {Id} does not have enough essence.", casterPlayer.PlayerId);
            return false;
        }

        // 2. O pagamento enviado pela UI cobre a fatura calculada?
        if (!ValidateAllocationAgainstCost(payment, calculatedCost.EssenceCosts))
        {
            _logger.LogWarning("Resource allocation provided does not match the calculated cost.");
            return false;
        }

        return true;
    }


    /// <summary>
    /// Deducts the essence from the player and HP from the combatant.
    /// </summary>
    private void ConsumeAbilityResources(
        Combatant source,
        GameState state,
        Dictionary<EssenceType, int> allocation,
        FinalAbilityCosts costs)
    {
        var player = state.Players.First(p => p.PlayerId == source.OwnerId);

        // Pagar Essence 
        _essenceService.ConsumeEssence(player, allocation);

        // Pagar HP 
        if (costs.HPCost > 0)
        {
            source.CurrentHP -= costs.HPCost;
            _logger.LogInformation("{Source} paid {HP} HP cost.", source.Name, costs.HPCost);
        }
    }

    /// <summary>
    /// Calculates final countdown value aind applies it to source.
    /// </summary>
    private void ApplyAbilityCooldown(Combatant source, AbilityDefinition ability)
    {
        int finalCooldownTurns = _cooldownCalcService.GetFinalCooldown(source, ability); 

        if (finalCooldownTurns > 0)
        {
            if (finalCooldownTurns > 0)
            {
                source.ActiveCooldowns.Add(
                    new ActiveCooldown { AbilityId = ability.Id, TurnsRemaining = finalCooldownTurns });
            }

            _logger.LogInformation(
                "Applied {Turns} turn(s) cooldown for Ability {AbilityId} to {SourceId}",
                finalCooldownTurns, ability.Id, source.Id);
        }
    }


    /// <summary>
    /// Verifies if the provided payment dictionary covers all costs in the invoice, 
    /// including neutral costs.
    /// </summary>
    private bool ValidateAllocationAgainstCost(Dictionary<EssenceType, int> payment, List<EssenceCost> requiredCost)
    {
        // Clonar pool para simular consumo
        var allocationPool = new Dictionary<EssenceType, int>(payment);

        // 1. Pagar coloridos específicos
        foreach (var cost in requiredCost.Where(c => c.Type != EssenceType.Neutral))
        {
            if (!allocationPool.TryGetValue(cost.Type, out int amount) || amount < cost.Amount)
                return false; // Falta cor específica no pagamento

            allocationPool[cost.Type] -= cost.Amount;
        }

        // 2. Pagar neutros com o que sobra
        int neutralNeeded = requiredCost.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);
        int remainingAllocation = allocationPool.Values.Sum();

        return remainingAllocation >= neutralNeeded;
    }
    
}