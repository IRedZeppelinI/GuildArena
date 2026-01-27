using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat;

/// <summary>
/// The main orchestrator for combat logic. 
/// Acts as a Queue Processor and Service Locator for individual Actions.
/// </summary>
public class CombatEngine : ICombatEngine
{
    private readonly IReadOnlyDictionary<EffectType, IEffectHandler> _handlers;
    private readonly IActionQueue _actionQueue;

    // --- Services Exposed to Actions (Service Locator) ---
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
    public IBattleLogService BattleLog { get; }

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
        IActionQueue actionQueue,
        IBattleLogService battleLog)
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
        BattleLog = battleLog;
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
    /// Executes a player's ability by scheduling the intention and processing the resulting action queue.    
    /// </summary>
    /// <returns>A list of results (one per action) containing Battle Logs for the UI.</returns>
    //public List<CombatActionResult> ExecuteAbility(
    //    GameState state,
    //    AbilityDefinition ability,
    //    Combatant source,
    //    AbilityTargets targets,
    //    Dictionary<EssenceType, int> payment)
    //{
    //    // 1. Limpar fila (garantia de estado limpo para o novo pedido)
    //    _actionQueue.Clear();

    //    // 2. Criar a ação raiz (A intenção do jogador)
    //    // Nota: O bool 'false' indica que é uma ação voluntária, não um trigger (respeita regras de morte)
    //    var rootAction = new ExecuteAbilityAction(ability, source, targets, payment, isTriggeredAction: false);
    //    _actionQueue.Enqueue(rootAction);

    //    // 3. Processar tudo até a fila esvaziar
    //    return ProcessQueue(state);
    //}

    /// <inheritdoc />
    public List<CombatActionResult> ExecuteAbility(
        GameState state,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> payment)
    {
        // 1. Limpar fila (garantia de estado limpo para o novo pedido)
        _actionQueue.Clear();

        // 2. Criar a ação raiz (A intenção do jogador)
        var rootAction = new ExecuteAbilityAction(ability, source, targets, payment, isTriggeredAction: false);
        _actionQueue.Enqueue(rootAction);

        // 3. Processar (Agora delegado no método partilhado)
        return ProcessPendingActions(state);
    }



    /// <inheritdoc />
    public List<CombatActionResult> ProcessPendingActions(GameState state)
    {
        var results = new List<CombatActionResult>();
        int safetyCounter = 0;
        const int MaxActionsPerTurn = 50;

        while (_actionQueue.HasNext())
        {
            if (safetyCounter++ > MaxActionsPerTurn)
            {
                AppLogger.LogError(
                    "Max actions per execution exceeded. Possible infinite trigger loop.");
                break;
            }

            var action = _actionQueue.Dequeue();
            if (action == null) continue;

            // Executar a Ação
            var result = action.Execute(this, state);

            results.Add(result);
        }

        return results;
    }

    //private List<CombatActionResult> ProcessQueue(GameState state)
    //{
    //    var results = new List<CombatActionResult>();
    //    int safetyCounter = 0;
    //    const int MaxActionsPerTurn = 50;

    //    while (_actionQueue.HasNext())
    //    {
    //        if (safetyCounter++ > MaxActionsPerTurn)
    //        {
    //            AppLogger.LogError(
    //                "Max actions per execution exceeded. Possible infinite trigger loop.");
    //            break;
    //        }

    //        var action = _actionQueue.Dequeue();
    //        if (action == null) continue;

            

    //        // Executar a Ação
    //        var result = action.Execute(this, state);

    //        results.Add(result);
    //    }

    //    return results;
    //}
}