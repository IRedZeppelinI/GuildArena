using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.Targeting;
using GuildArena.Domain.ValueObjects.State;
using Microsoft.Extensions.Logging;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;

namespace GuildArena.Core.Combat.Actions;

/// <summary>
/// Encapsulates the complete logic for executing a specific ability during combat.
/// Handles validation, costs, cooldowns, logs, and effect application.
/// </summary>
public class ExecuteAbilityAction : ICombatAction
{
    // Dados imutáveis da intenção original
    private readonly AbilityDefinition _ability;
    private readonly AbilityTargets _userSelectedTargets;
    private readonly Dictionary<EssenceType, int> _payment;    

    private bool _isTriggeredAction;

    public string Name => $"Execute Ability: {_ability.Id}";
    public Combatant Source { get; }

    public ExecuteAbilityAction(
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets userSelectedTargets,
        Dictionary<EssenceType, int> payment,        
        bool isTriggeredAction = false)
    {
        _ability = ability;
        Source = source;
        _userSelectedTargets = userSelectedTargets;
        _payment = payment;
        _isTriggeredAction = isTriggeredAction;        
    }

    public CombatActionResult Execute(ICombatEngine engine, GameState gameState)
    {
        var result = new CombatActionResult();

        // 1. App Log 
        engine.AppLogger.LogInformation(
            "Processing Action: {ActionName} for Source {SourceId}", Name, Source.Id);

        // 2. Resolve Targets (Converte Input UI -> Combatentes Reais)
        var resolvedTargets = ResolveInitialTargets(engine, gameState);

        // 3. Validações (Custos, Status, Cooldowns)
        if (!CanExecute(engine, gameState, resolvedTargets, out var calculatedCost, result))
        {
            result.IsSuccess = false;
            return result;
        }

        // 4. Pagamento e Consumo
        PayCosts(engine, gameState, calculatedCost);

        // --- BATTLE LOG (Para o Cliente) ---        
        engine.BattleLog.Log($"{Source.Name} used {_ability.Name}!");

        // 5. Trigger: ON_ABILITY_CAST        
        var castContext = new TriggerContext
        {
            Source = Source,
            Target = null,
            GameState = gameState,
            Tags = new HashSet<string>(_ability.Tags, StringComparer.OrdinalIgnoreCase)
        };
        engine.TriggerProcessor.ProcessTriggers(ModifierTrigger.ON_ABILITY_CAST, castContext);

        // 6. Aplicar Cooldown
        ApplyCooldown(engine);

        // 7. Executar Efeitos
        ProcessEffects(engine, gameState, result);

        return result;
    }

    // --- Lógica Interna ---

    private List<Combatant> ResolveInitialTargets(ICombatEngine engine, GameState state)
    {
        var list = new List<Combatant>();
        foreach (var rule in _ability.TargetingRules)
        {
            list.AddRange(engine.TargetService.ResolveTargets(rule, Source, state, _userSelectedTargets));
        }
        return list;
    }

    private bool CanExecute(
        ICombatEngine engine,
        GameState state,
        List<Combatant> targets,
        out FinalAbilityCosts calculatedCost,
        CombatActionResult result)
    {
        calculatedCost = null!;

        if (!Source.IsAlive && !_isTriggeredAction)
        {
            engine.AppLogger.LogWarning("{Source} is dead and cannot act.", Source.Name);
            return false;
        }

        // Validação de Status (Stun, Silence)
        var statusResult = engine.StatusService.CheckStatusConditions(Source, _ability);
        if (statusResult != ActionStatusResult.Allowed)
        {
            engine.AppLogger.LogWarning(
                "{Source} failed status check: {Reason}", Source.Name, statusResult);
            engine.BattleLog.Log($"{Source.Name} tries to act but is {statusResult}!");
            return false;
        }

        // Validação de Action Points
        if (_ability.ActionPointCost > 0)
        {
            int maxActions = (int)engine.StatService.GetStatValue(Source, StatType.MaxActions);
            if (Source.ActionsTakenThisTurn + _ability.ActionPointCost > maxActions)
            {
                engine.AppLogger.LogWarning("{Source} insufficient Action Points.", Source.Name);
                return false;
            }
        }

        // Validação de Cooldown
        var existingCooldown = Source.ActiveCooldowns.FirstOrDefault(c => c.AbilityId == _ability.Id);
        if (existingCooldown != null)
        {
            engine.AppLogger.LogWarning("{Source} ability on cooldown.", Source.Name);
            return false;
        }

        // Cálculo e Validação de Custos (Essence/HP)
        var player = state.Players.First(p => p.PlayerId == Source.OwnerId);

        
        calculatedCost = engine.CostService.CalculateFinalCosts(
            player,
            _ability,
            targets,
            _userSelectedTargets);
        

        if (calculatedCost.HPCost > 0 && Source.CurrentHP <= calculatedCost.HPCost) return false;
        if (!engine.EssenceService.HasEnoughEssence(player, calculatedCost.EssenceCosts)) return false;

        // Validação da Alocação (Pagamento da UI vs Fatura Real)
        if (!ValidateAllocation(calculatedCost.EssenceCosts))
        {
            engine.AppLogger.LogWarning("Invalid resource allocation.");
            return false;
        }
        
        //verificar se jogador tem a essence que enviou para pagar
        if (!ValidatePaymentOwnership(player, _payment))
        {
            engine.AppLogger.LogWarning("Player tried to pay with essence they do not possess.");
            return false;
        }

        return true;
    }

    private bool ValidateAllocation(List<EssenceAmount> required)
    {
        var pool = new Dictionary<EssenceType, int>(_payment);

        foreach (var req in required.Where(c => c.Type != EssenceType.Neutral))
        {
            if (!pool.TryGetValue(req.Type, out int val) || val < req.Amount) return false;
            pool[req.Type] -= req.Amount;
        }

        int neutralNeeded = required.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);
        return pool.Values.Sum() >= neutralNeeded;
    }

    private bool ValidatePaymentOwnership(CombatPlayer player, Dictionary<EssenceType, int> payment)
    {
        foreach (var item in payment)
        {
            // Ignora pagamentos de 0
            if (item.Value <= 0) continue;
            
            
            if (!player.EssencePool.TryGetValue(item.Key, out int currentAmount) || 
                currentAmount < item.Value)
            {
                return false;
            }
        }
        return true;
    }

    private void PayCosts(ICombatEngine engine, GameState state, FinalAbilityCosts costs)
    {
        var player = state.Players.First(p => p.PlayerId == Source.OwnerId);
        engine.EssenceService.ConsumeEssence(player, _payment);

        if (costs.HPCost > 0)
        {
            Source.CurrentHP -= costs.HPCost;
        }

        if (_ability.ActionPointCost > 0)
        {
            Source.ActionsTakenThisTurn += _ability.ActionPointCost;
        }
    }

    private void ApplyCooldown(ICombatEngine engine)
    {
        int turns = engine.CooldownService.GetFinalCooldown(Source, _ability);
        if (turns > 0)
        {
            Source.ActiveCooldowns.Add(
                new ActiveCooldown { AbilityId = _ability.Id, TurnsRemaining = turns });
        }
    }

    private void ProcessEffects(ICombatEngine engine, GameState state, CombatActionResult result)
    {
        var evasionCache = new Dictionary<int, bool>();

        foreach (var effect in _ability.Effects)
        {
            var handler = engine.GetEffectHandler(effect.Type);
            var rule = _ability.TargetingRules.FirstOrDefault(r => r.RuleId == effect.TargetRuleId);
            if (rule == null) continue;

            var targets = engine.TargetService.ResolveTargets(rule, Source, state, _userSelectedTargets);

            foreach (var target in targets)
            {
                if (IsInvulnerable(target))
                {
                    engine.BattleLog.Log($"{target.Name} is invulnerable!");
                    continue;
                }

                if (effect.CanBeEvaded)
                {
                    if (!evasionCache.TryGetValue(target.Id, out bool hit))
                    {
                        float chance = engine.HitChanceService.CalculateHitChance(Source, target, effect);
                        hit = engine.Random.NextDouble() < chance;
                        evasionCache[target.Id] = hit;

                        if (!hit)
                        {
                            engine.BattleLog.Log($"{Source.Name} missed {target.Name}!");
                            result.ResultTags.Add("Miss");
                        }
                    }

                    if (!hit) continue;
                }

                handler.Apply(effect, Source, target, state, result);
            }
        }
    }

    private bool IsInvulnerable(Combatant target)
    {
        return target.ActiveModifiers.Any(m => m.ActiveStatusEffects.Contains(StatusEffectType.Invulnerable));
    }
}