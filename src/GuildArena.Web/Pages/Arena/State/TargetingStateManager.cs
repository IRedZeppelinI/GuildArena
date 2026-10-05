using GuildArena.Domain.Enums.Targeting;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Web.Pages.Arena.State;

/// <summary>
/// Manages the UI state during the target selection phase of combat.
/// Relies purely on the pre-calculated ValidTargetIds provided by the Backend.
/// </summary>
public class TargetingStateManager
{
    public int? SourceId { get; private set; }
    public AbilitySummaryDto? ActiveAbility { get; private set; }

    public Dictionary<string, List<int>> SelectedTargets { get; private set; } = new();

    public bool IsActive => SourceId.HasValue && ActiveAbility != null;

    // Self, area effects and automatic strategies are resolved by the server.
    // A singleton enemy/friendly candidate is still a manual choice.
    public bool RequiresTargetSelection => IsActive && ActiveAbility!.TargetingRules.Any(RequiresManualSelection);
    public TargetingRuleDto? PendingRule => ActiveAbility?.TargetingRules.Where(RequiresManualSelection)
        .FirstOrDefault(r => !SelectedTargets.TryGetValue(r.RuleId, out var list) || list.Count < r.Count);
    public bool IsComplete => IsActive && PendingRule is null;
    public int PendingRuleSelectedCount => PendingRule is { } rule && SelectedTargets.TryGetValue(rule.RuleId, out var list) ? list.Count : 0;
    public int ManualRuleCount => ActiveAbility?.TargetingRules.Count(RequiresManualSelection) ?? 0;
    public int PendingRuleNumber => ActiveAbility?.TargetingRules.Where(RequiresManualSelection).ToList().FindIndex(r => r == PendingRule) + 1 ?? 0;

    public void StartTargeting(int sourceId, AbilitySummaryDto ability)
    {
        SourceId = sourceId;
        ActiveAbility = ability;
        SelectedTargets.Clear();
    }

    public void Cancel()
    {
        SourceId = null;
        ActiveAbility = null;
        SelectedTargets.Clear();
    }

    public bool IsSelected(int combatantId)
    {
        // Selection is per rule. The same combatant can satisfy distinct rules on the server.
        return PendingRule is { } rule
            ? SelectedTargets.TryGetValue(rule.RuleId, out var list) && list.Contains(combatantId)
            : SelectedTargets.Values.Any(list => list.Contains(combatantId));
    }

    /// <summary>
    /// Evaluates if a given combatant is a valid target by checking the pre-calculated list from the server.
    /// </summary>
    public bool IsValidTarget(CombatantDto target)
    {
        if (!IsActive) return false;

        return PendingRule is { } rule && rule.ValidTargetIds.Contains(target.Id) && !IsSelected(target.Id);
    }

    /// <summary>
    /// Attempts to register a click on a target. 
    /// Outputs true if all required targets for the ability have been met.
    /// </summary>
    public bool TrySelectTarget(CombatantDto target, out bool allRulesSatisfied)
    {
        allRulesSatisfied = false;

        if (!IsValidTarget(target))
            return false;

        var rule = PendingRule!;
        if (!SelectedTargets.TryGetValue(rule.RuleId, out var targetList))
        {
            targetList = new List<int>();
            SelectedTargets[rule.RuleId] = targetList;
        }
        targetList.Add(target.Id);

        allRulesSatisfied = IsComplete;

        return true;
    }

    /// <summary>Replace stale eligibility and retain only selections authorized by this snapshot.</summary>
    public bool Refresh(GameStateDto? state, int localPlayerId)
    {
        if (!IsActive) return false;
        var source = state?.Combatants.FirstOrDefault(c => c.Id == SourceId);
        var ability = source?.Abilities.FirstOrDefault(a => a.Id == ActiveAbility!.Id)
            ?? (source?.SpecialAbility?.Id == ActiveAbility!.Id ? source.SpecialAbility : null);
        if (state?.CurrentPlayerId != localPlayerId || source is null || source.OwnerId != localPlayerId ||
            !source.IsAlive || ability is null || !ability.IsAffordable || ability.CurrentCooldownTurns > 0)
        {
            Cancel();
            return false;
        }
        ActiveAbility = ability;
        SelectedTargets = ability.TargetingRules.Where(RequiresManualSelection)
            .Where(r => SelectedTargets.ContainsKey(r.RuleId))
            .ToDictionary(r => r.RuleId, r => SelectedTargets[r.RuleId]
                .Where(id => r.ValidTargetIds.Contains(id) && state.Combatants.Any(c => c.Id == id))
                .Distinct().Take(r.Count).ToList());
        return true;
    }

    private static bool RequiresManualSelection(TargetingRuleDto rule) =>
        rule.Strategy == TargetSelectionStrategy.Manual && rule.Count > 0 &&
        rule.Type != TargetType.Self && !IsAoE(rule.Type);

    private static bool IsAoE(TargetType type)
    {
        return type == TargetType.All || type == TargetType.AllEnemies ||
               type == TargetType.AllAllies || type == TargetType.AllFriendlies;
    }
}
