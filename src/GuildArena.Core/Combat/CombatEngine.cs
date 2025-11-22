using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
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

    public CombatEngine(
        IEnumerable<IEffectHandler> handlers,
        ILogger<CombatEngine> logger,
        ICooldownCalculationService cooldownCalcService,
        ICostCalculationService costCalcService, 
        IEssenceService essenceService)
    {
        // O construtor (via DI) recebe *todos* os handlers e organiza-os
        // num dicionário para acesso instantâneo.
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
        _logger = logger;
        _cooldownCalcService = cooldownCalcService;
        _costCalcService = costCalcService;
        _essenceService = essenceService;
    }

    /// <inheritdoc />
    public void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> payment)
    {

        // 1. RESOLVER ALVOS (Necessário para calcular custos de Ward)
        // (Nota: Movemos isto para cima porque o CostCalculator precisa dos alvos)
        var resolvedTargets = new List<Combatant>();
        foreach (var rule in ability.TargetingRules)
        {
            resolvedTargets.AddRange(GetTargetsForRule(rule, source, currentState, targets));
        }

        // 2. VERIFICAR PRÉ-CONDIÇÕES (Cooldowns, Custos, Pagamentos)
        // Agora passamos o 'payment' e 'resolvedTargets' para validação
        if (!CanExecuteAbility(source, currentState, ability, resolvedTargets, payment, out var finalCosts))
        {
            return; // O helper já logou o motivo
        }

        _logger.LogInformation("Executing ability {AbilityId} from {SourceId}", ability.Id, source.Id);

        // 3. PAGAR CUSTOS (Essence e HP)
        PayAbilityCosts(source, currentState, payment, finalCosts);

        // 4. APLICAR COOLDOWN
        ApplyAbilityCooldown(source, ability);

        // 5. RESOLVER EFEITOS
        // Como já resolvemos os alvos no passo 1, podemos otimizar isto,
        // mas mantemos o loop original por agora para respeitar a estrutura dos effects.
        foreach (var effect in ability.Effects)
        {
            if (!_handlers.TryGetValue(effect.Type, out var handler))
            {
                _logger.LogWarning("No IEffectHandler found for {Type}", effect.Type);
                continue;
            }

            // Re-obter alvos para este efeito específico
            // (Poderíamos otimizar usando o 'resolvedTargets' se mapeado por RuleId)
            var rule = ability.TargetingRules.FirstOrDefault(r => r.RuleId == effect.TargetRuleId);
            if (rule == null) continue;

            var effectTargets = GetTargetsForRule(rule, source, currentState, targets);

            foreach (var target in effectTargets)
            {
                handler.Apply(effect, source, target);
            }
        }
    }

    
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
        out FinalAbilityCosts finalCosts)
    {
        finalCosts = null!;

        // A. Cooldowns
        var existingCooldown = source.ActiveCooldowns.FirstOrDefault(c => c.AbilityId == ability.Id);
        if (existingCooldown != null)
        {
            _logger.LogWarning("Ability {Id} on cooldown ({Turns} turns).", ability.Id, existingCooldown.TurnsRemaining);
            return false;
        }

        // B. Calcular Custos (Calculate Invoice)
        var casterPlayer = state.Players.First(p => p.PlayerId == source.OwnerId);
        finalCosts = _costCalcService.CalculateFinalCosts(casterPlayer, ability, targets);

        // C. Validar Saldo de HP (Validate HP Balance)
        if (source.CurrentHP <= finalCosts.HPCost)
        {
            _logger.LogWarning("Not enough HP to pay cost. HP: {HP}, Cost: {Cost}", source.CurrentHP, finalCosts.HPCost);
            return false;
        }

        // D. Validar Essence (Validate Essence Balance & Payment)
        // 1. O jogador tem dinheiro suficiente no banco?
        if (!_essenceService.HasEnoughEssence(casterPlayer, finalCosts.EssenceCosts))
        {
            _logger.LogWarning("Player {Id} does not have enough essence.", casterPlayer.PlayerId);
            return false;
        }

        // 2. O pagamento enviado pela UI cobre a fatura calculada?
        if (!ValidatePaymentAgainstInvoice(payment, finalCosts.EssenceCosts))
        {
            _logger.LogWarning("Payment provided does not match the calculated cost.");
            return false;
        }

        return true;
    }


    /// <summary>
    /// Deducts the essence from the player and HP from the combatant.
    /// </summary>
    private void PayAbilityCosts(
        Combatant source,
        GameState state,
        Dictionary<EssenceType, int> payment,
        FinalAbilityCosts costs)
    {
        var player = state.Players.First(p => p.PlayerId == source.OwnerId);

        // Pagar Essence (via Serviço)
        _essenceService.PayEssence(player, payment);

        // Pagar HP (Diretamente no Combatant)
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
    private bool ValidatePaymentAgainstInvoice(Dictionary<EssenceType, int> payment, List<EssenceCost> invoice)
    {
        // Clonar pagamento para simular consumo
        var paymentPool = new Dictionary<EssenceType, int>(payment);

        // 1. Pagar coloridos específicos
        foreach (var cost in invoice.Where(c => c.Type != EssenceType.Neutral))
        {
            if (!paymentPool.TryGetValue(cost.Type, out int amount) || amount < cost.Amount)
                return false; // Falta cor específica no pagamento

            paymentPool[cost.Type] -= cost.Amount;
        }

        // 2. Pagar neutros com o que sobra
        int neutralNeeded = invoice.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);
        int paymentLeft = paymentPool.Values.Sum();

        return paymentLeft >= neutralNeeded;
    }


    private List<Combatant> GetTargetsForRule(
        TargetingRule rule,
        Combatant source,
        GameState currentState,
        AbilityTargets abilityTargets)
    {
        // Obter a lista base de alvos 
        List<Combatant> baseTargetList;

        switch (rule.Type)
        {
            // Casos de "Clique" (usam o 'mapa' da UI)
            case TargetType.Enemy:
            case TargetType.Ally:
            case TargetType.Friendly:
                {
                    if (!abilityTargets.SelectedTargets.TryGetValue(rule.RuleId, out var selectedTargetIds))
                    {
                        _logger.LogWarning("No targets provided by UI for TargetRuleId '{RuleId}'", rule.RuleId);
                        return new List<Combatant>();
                    }
                    baseTargetList = currentState.Combatants
                        .Where(c => selectedTargetIds.Contains(c.Id))
                        .ToList();
                    break;
                }

            // Casos de "Não Clique" (AoE / Self)
            case TargetType.Self:
                baseTargetList = new List<Combatant> { source };
                break;
            case TargetType.AllEnemies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId != source.OwnerId)
                    .ToList();
                break;
            case TargetType.AllAllies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId && c.Id != source.Id)
                    .ToList();
                break;

            case TargetType.AllFriendlies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId)
                    .ToList();
                break;
            default:
                baseTargetList = new List<Combatant>();
                break;
        }

        // Aplicar Filtros de Validação 
        List<Combatant> validatedTargets = new();

        // Filtro de "Vivo/Morto"
        if (!rule.CanTargetDead)
        {            
            validatedTargets = baseTargetList.Where(c => c.IsAlive).ToList();
        }
        else
        {
            // Regra de "Reviver": Só pode atingir alvos mortos.            
            validatedTargets = baseTargetList.Where(c => !c.IsAlive).ToList();
        }

        // Filtro de Tipo (Inimigo/Aliado) 
        switch (rule.Type)
        {
            case TargetType.Enemy:
            case TargetType.AllEnemies:
                return validatedTargets
                    .Where(t => t.OwnerId != source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Ally:
            case TargetType.AllAllies:
                return validatedTargets
                    .Where(t => t.OwnerId == source.OwnerId && t.Id != source.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Friendly:
            case TargetType.AllFriendlies:
                return validatedTargets
                    .Where(t => t.OwnerId == source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Self:
                return validatedTargets; // Já é 'source', não precisa de mais filtros

            default:
                return new List<Combatant>();
        }
    }
}