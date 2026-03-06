using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.Targeting;

namespace GuildArena.Application.Combat.AI.Behaviors;

public class RandomAiBehavior : IAiBehavior
{
    private readonly IStatCalculationService _statService;
    private readonly IStatusConditionService _statusService;
    private readonly ITargetResolutionService _targetService;
    private readonly ICostCalculationService _costService;
    private readonly IEssenceService _essenceService;
    private readonly IRandomProvider _random;

    public RandomAiBehavior(
        IStatCalculationService statService,
        IStatusConditionService statusService,
        ITargetResolutionService targetService,
        ICostCalculationService costService,
        IEssenceService essenceService,
        IRandomProvider random)
    {
        _statService = statService;
        _statusService = statusService;
        _targetService = targetService;
        _costService = costService;
        _essenceService = essenceService;
        _random = random;
    }

    public AiActionIntent? DecideNextAction(GameState gameState, int aiPlayerId)
    {
        var aiPlayer = gameState.Players.First(p => p.PlayerId == aiPlayerId);

        var activeCombatants = gameState.Combatants
            .Where(c => c.OwnerId == aiPlayerId && c.IsAlive)
            .OrderBy(x => _random.NextDouble())
            .ToList();

        foreach (var combatant in activeCombatants)
        {
            int maxActions = (int)_statService.GetStatValue(combatant, StatType.MaxActions);
            if (combatant.ActionsTakenThisTurn >= maxActions) continue;

            var allAbilities = combatant.Abilities.ToList();
            if (combatant.SpecialAbility != null) allAbilities.Add(combatant.SpecialAbility);

            allAbilities = allAbilities.OrderBy(x => _random.NextDouble()).ToList();

            foreach (var ability in allAbilities)
            {
                if (combatant.ActiveCooldowns.Any(c => c.AbilityId == ability.Id)) continue;
                if (_statusService.CheckStatusConditions(combatant, ability) != ActionStatusResult.Allowed) continue;

                var targetSelections = new Dictionary<string, List<int>>();
                var flattenedTargets = new List<Combatant>();
                bool hasValidTargets = true;

                foreach (var rule in ability.TargetingRules)
                {
                    // A MAGIA ESTÁ AQUI: Usamos o método novo para "espreitar" as opções
                    var validTargets = _targetService.GetValidCandidates(rule, combatant, gameState);

                    if (rule.Strategy == TargetSelectionStrategy.Manual && rule.Count > 0)
                    {
                        if (validTargets.Count < rule.Count)
                        {
                            hasValidTargets = false;
                            break;
                        }
                        // IA escolhe aleatoriamente de entre os válidos
                        validTargets = validTargets.OrderBy(x => _random.NextDouble()).Take(rule.Count).ToList();
                    }

                    targetSelections[rule.RuleId] = validTargets.Select(t => t.Id).ToList();
                    flattenedTargets.AddRange(validTargets);
                }

                if (!hasValidTargets) continue;

                // Calcula os Custos usando o fake input gerado pela IA
                var fakeInput = new AbilityTargets { SelectedTargets = targetSelections };
                var invoice = _costService.CalculateFinalCosts(aiPlayer, ability, flattenedTargets, fakeInput);

                if (invoice.HPCost > 0 && combatant.CurrentHP <= invoice.HPCost) continue;
                if (!_essenceService.HasEnoughEssence(aiPlayer, invoice.EssenceCosts)) continue;

                var payment = GeneratePayment(aiPlayer, invoice.EssenceCosts);
                if (payment == null) continue;

                return new AiActionIntent
                {
                    SourceId = combatant.Id,
                    AbilityId = ability.Id,
                    TargetSelections = targetSelections,
                    Payment = payment
                };
            }
        }

        return null;
    }

    private Dictionary<EssenceType, int>? GeneratePayment(CombatPlayer player, List<EssenceAmount> costs)
    {
        var payment = new Dictionary<EssenceType, int>();
        var poolCopy = new Dictionary<EssenceType, int>(player.EssencePool);

        foreach (var cost in costs.Where(c => c.Type != EssenceType.Neutral))
        {
            payment[cost.Type] = cost.Amount;
            poolCopy[cost.Type] -= cost.Amount;
        }

        int neutralNeeded = costs.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);

        while (neutralNeeded > 0)
        {
            var availableColor = poolCopy.FirstOrDefault(kvp => kvp.Value > 0).Key;
            if (availableColor == default) return null;

            if (!payment.ContainsKey(availableColor)) payment[availableColor] = 0;

            payment[availableColor]++;
            poolCopy[availableColor]--;
            neutralNeeded--;
        }

        return payment;
    }
}