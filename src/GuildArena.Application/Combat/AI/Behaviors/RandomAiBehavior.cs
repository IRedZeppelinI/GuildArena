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

        // 1. Obter todos os monstros da IA vivos e baralhá-los
        var activeCombatants = gameState.Combatants
            .Where(c => c.OwnerId == aiPlayerId && c.IsAlive)
            .OrderBy(x => _random.NextDouble())
            .ToList();

        foreach (var combatant in activeCombatants)
        {
            // Verifica se ainda tem Action Points
            int maxActions = (int)_statService.GetStatValue(combatant, StatType.MaxActions);
            if (combatant.ActionsTakenThisTurn >= maxActions) continue;

            // Junta todas as habilidades (Normais + Especial/Guard)
            var allAbilities = combatant.Abilities.ToList();
            if (combatant.SpecialAbility != null) allAbilities.Add(combatant.SpecialAbility);

            // Baralha as habilidades para não usar sempre a primeira
            allAbilities = allAbilities.OrderBy(x => _random.NextDouble()).ToList();

            foreach (var ability in allAbilities)
            {
                // A) Verifica Cooldown
                if (combatant.ActiveCooldowns.Any(c => c.AbilityId == ability.Id)) continue;

                // B) Verifica Status (Stun, Silence, Disarm)
                if (_statusService.CheckStatusConditions(combatant, ability) != ActionStatusResult.Allowed) continue;

                // C) Resolve Alvos
                var targetSelections = new Dictionary<string, List<int>>();
                var flattenedTargets = new List<Combatant>();
                bool hasValidTargets = true;

                foreach (var rule in ability.TargetingRules)
                {
                    // Pede ao serviço a lista de alvos válidos (passando input vazio pois a IA ainda não escolheu)
                    var validTargets = _targetService.ResolveTargets
                        (rule, combatant, gameState, new AbilityTargets());

                    // Filtragem manual para a IA: Se for preciso escolher 1 e houver válidos, ela escolhe aleatoriamente
                    if (rule.Strategy == TargetSelectionStrategy.Manual && rule.Count > 0)
                    {
                        if (validTargets.Count < rule.Count)
                        {
                            hasValidTargets = false;
                            break; // Não há alvos suficientes
                        }
                        validTargets = validTargets.OrderBy(x => _random.NextDouble()).Take(rule.Count).ToList();
                    }

                    targetSelections[rule.RuleId] = validTargets.Select(t => t.Id).ToList();
                    flattenedTargets.AddRange(validTargets);
                }

                if (!hasValidTargets) continue; // Tenta a próxima habilidade

                // D) Calcula Custos
                var fakeInput = new AbilityTargets { SelectedTargets = targetSelections };
                var invoice = _costService.CalculateFinalCosts(aiPlayer, ability, flattenedTargets, fakeInput);

                // Anti-Suicídio: A IA burra não usa habilidades que a matariam (ex: Vex Blood Fuel com pouco HP)
                if (invoice.HPCost > 0 && combatant.CurrentHP <= invoice.HPCost) continue;

                // Verifica se tem Essence suficiente
                if (!_essenceService.HasEnoughEssence(aiPlayer, invoice.EssenceCosts)) continue;

                // E) Gera o Pagamento exato
                var payment = GeneratePayment(aiPlayer, invoice.EssenceCosts);
                if (payment == null) continue; // Falha de segurança no cálculo de neutral

                // SUCESSO! Encontrámos uma jogada válida!
                return new AiActionIntent
                {
                    SourceId = combatant.Id,
                    AbilityId = ability.Id,
                    TargetSelections = targetSelections,
                    Payment = payment
                };
            }
        }

        // Se chegámos aqui, iterámos todos os monstros e habilidades e não há nada a fazer.
        return null;
    }

    /// <summary>
    /// Helper to convert the cost invoice into a specific payment dictionary.
    /// Handles spending colored essence first, then fills neutral costs with leftovers.
    /// </summary>
    private Dictionary<EssenceType, int>? GeneratePayment(CombatPlayer player, List<EssenceAmount> costs)
    {
        var payment = new Dictionary<EssenceType, int>();
        var poolCopy = new Dictionary<EssenceType, int>(player.EssencePool);

        // 1. Pagar os custos coloridos específicos
        foreach (var cost in costs.Where(c => c.Type != EssenceType.Neutral))
        {
            payment[cost.Type] = cost.Amount;
            poolCopy[cost.Type] -= cost.Amount;
        }

        // 2. Pagar o custo Neutral com o que sobrar (escolhe cores aleatórias)
        int neutralNeeded = costs.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);

        while (neutralNeeded > 0)
        {
            // Encontra uma essência que a IA ainda tenha
            var availableColor = poolCopy.FirstOrDefault(kvp => kvp.Value > 0).Key;

            // Isto não devia acontecer porque o HasEnoughEssence passou, mas fica a salvaguarda
            if (availableColor == default) return null;

            if (!payment.ContainsKey(availableColor)) payment[availableColor] = 0;

            payment[availableColor]++;
            poolCopy[availableColor]--;
            neutralNeeded--;
        }

        return payment;
    }
}