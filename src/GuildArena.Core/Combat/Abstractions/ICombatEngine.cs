using GuildArena.Core.Combat.Actions; // Agora já é visível
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Abstractions; 

public interface ICombatEngine
{
    // --- Services Exposed to Actions (Service Locator Pattern) ---
    
    ILogger<ICombatEngine> AppLogger { get; }

    ICooldownCalculationService CooldownService { get; }
    ICostCalculationService CostService { get; }
    IEssenceService EssenceService { get; }
    ITargetResolutionService TargetService { get; }
    IStatusConditionService StatusService { get; }
    IHitChanceService HitChanceService { get; }
    IRandomProvider Random { get; }
    IStatCalculationService StatService { get; }
    ITriggerProcessor TriggerProcessor { get; }
    IBattleLogService BattleLog { get; }

    /// <summary>
    /// Retrieves the specific handler for a given effect type.
    /// </summary>
    IEffectHandler GetEffectHandler(EffectType type);


    /// <summary>
    /// Executes a player's ability by scheduling the intention and processing the resulting action queue.
    /// </summary>
    List<CombatActionResult> ExecuteAbility(
        GameState state,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets,
        Dictionary<EssenceType, int> payment);



    // --- Queue Management ---
    void EnqueueAction(ICombatAction action);


    /// <summary>
    /// Processes all actions currently waiting in the Action Queue until it is empty.
    /// Useful for processing system-generated triggers (like Start of Combat) that occur outside of a player's action.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <returns>A list of results from the processed actions.</returns>
    List<CombatActionResult> ProcessPendingActions(GameState state);
}