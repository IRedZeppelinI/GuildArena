using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat;

/// <summary>
/// The main orchestrator for combat actions. 
/// Acts as a Queue Processor and Service Locator for individual Actions.
/// </summary>
public class CombatEngine : ICombatEngine
{
    private readonly IReadOnlyDictionary<EffectType, IEffectHandler> _handlers;
    private readonly IActionQueue _actionQueue;

    // --- Serviços Expostos ---
    public ILogger<ICombatEngine> AppLogger { get; }
    public ICooldownCalculationService CooldownService { get; }
    public ICostCalculationService CostService { get; }
    public IEssenceService EssenceService { get; }
    public ITargetResolutionService TargetService { get; }
    public IStatusConditionService StatusService { get; }
    public IHitChanceService HitChanceService { get; }
    public IRandomProvider Random { get; }
    public IStatCalculationService StatService { get; }
    public ITriggerProcessor TriggerProcessor { get; }

    public CombatEngine(
        IEnumerable<IEffectHandler> handlers,
        ILogger<CombatEngine> logger,
        ICooldownCalculationService cooldownCalcService,
        ICostCalculationService costCalcService,
        IEssenceService essenceService,
        ITargetResolutionService targetService,
        IStatusConditionService statusService,
        IHitChanceService hitChanceService,
        IRandomProvider random,
        IStatCalculationService statService,
        ITriggerProcessor triggerProcessor,
        IActionQueue actionQueue)
    {
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
        AppLogger = logger;
        CooldownService = cooldownCalcService;
        CostService = costCalcService;
        EssenceService = essenceService;
        TargetService = targetService;
        StatusService = statusService;
        HitChanceService = hitChanceService;
        Random = random;
        StatService = statService;
        TriggerProcessor = triggerProcessor;
        _actionQueue = actionQueue;
    }

    /// <inheritdoc />
    public IEffectHandler GetEffectHandler(EffectType type)
    {
        return _handlers[type];
    }

    /// <inheritdoc />
    public void EnqueueAction(ICombatAction action)
    {
        _actionQueue.Enqueue(action);
    }

    /// <summary>
    /// Entry point for the API. Schedules the player's intention and processes the entire resulting chain.
    /// </summary>
    /// <returns>A list of results (one per action) containing Battle Logs for the UI.</returns>
    public List<CombatActionResult> ProcessTurnAction(
        GameState state,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> payment)
    {
        // 1. Limpar fila (garantia de estado limpo para o novo pedido)
        _actionQueue.Clear();

        // 2. Criar a ação raiz (A intenção do jogador)
        var rootAction = new ExecuteAbilityAction(ability, source, targets, payment);
        _actionQueue.Enqueue(rootAction);

        // 3. Processar tudo até a fila esvaziar
        return ProcessQueue(state);
    }

    // --- Compatibilidade / Legacy (Opcional) ---
    // Podes manter este método se ainda tiveres código antigo a chamá-lo, mas deve ser removido brevemente.
    //public void ExecuteAbility(
    //    GameState state,
    //    AbilityDefinition ability,
    //    Combatant source,
    //    AbilityTargets targets,
    //    Dictionary<EssenceType,
    //        int> payment)
    //{
    //    ProcessTurnAction(state, ability, source, targets, payment);
    //}

    private List<CombatActionResult> ProcessQueue(GameState state)
    {
        var results = new List<CombatActionResult>();
        int safetyCounter = 0;
        const int MaxActionsPerTurn = 50; // Proteção contra loops infinitos de triggers (ex: 2 gajos com thorns a baterem um no outro)

        while (_actionQueue.HasNext())
        {
            if (safetyCounter++ > MaxActionsPerTurn)
            {
                AppLogger.LogError("Max actions per turn exceeded. Possible infinite trigger loop.");
                break;
            }

            var action = _actionQueue.Dequeue();
            if (action == null) continue;

            // Verificar morte antes de executar
            // Se a fonte da ação morreu entretanto
            // (ex: morreu no trigger anterior), a ação falha ou é ignorada
            if (!action.Source.IsAlive)
            {
                AppLogger.LogInformation(
                    "Action {Action} skipped because source {Source} is dead.",
                    action.Name,
                    action.Source.Name);
                continue;
            }

            // Executar a Ação
            // Passamos 'this' porque o Engine implementa ICombatEngine que expõe os serviços necessários
            var result = action.Execute(this, state);

            results.Add(result);
        }

        return results;
    }
}