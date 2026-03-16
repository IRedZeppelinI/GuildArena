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
        return SelectedTargets.Values.Any(list => list.Contains(combatantId));
    }

    /// <summary>
    /// Evaluates if a given combatant is a valid target by checking the pre-calculated list from the server.
    /// </summary>
    public bool IsValidTarget(CombatantDto target)
    {
        if (!IsActive) return false;

        foreach (var rule in ActiveAbility!.TargetingRules.Where(r => r.Strategy == TargetSelectionStrategy.Manual))
        {
            int currentSelectedCount = SelectedTargets.TryGetValue(rule.RuleId, out var list) ? list.Count : 0;

            if (currentSelectedCount < rule.Count)
            {
                // O Backend já fez a matemática do Taunt, Stealth e Raças!
                // Basta verificar se o ID do alvo está na lista de autorizados.
                return rule.ValidTargetIds.Contains(target.Id);
            }
        }
        return false;
    }

    /// <summary>
    /// Attempts to register a click on a target. 
    /// Outputs true if all required targets for the ability have been met.
    /// </summary>
    public bool TrySelectTarget(CombatantDto target, out bool allRulesSatisfied)
    {
        allRulesSatisfied = false;

        if (!IsActive || !IsValidTarget(target) || IsSelected(target.Id))
            return false;

        foreach (var rule in ActiveAbility!.TargetingRules.Where(r => r.Strategy == TargetSelectionStrategy.Manual))
        {
            if (!SelectedTargets.TryGetValue(rule.RuleId, out var targetList))
            {
                targetList = new List<int>();
                SelectedTargets[rule.RuleId] = targetList;
            }

            if (targetList.Count < rule.Count)
            {
                targetList.Add(target.Id);
                break;
            }
        }

        allRulesSatisfied = ActiveAbility!.TargetingRules
            .Where(r => r.Strategy == TargetSelectionStrategy.Manual)
            .All(r => SelectedTargets.TryGetValue(r.RuleId, out var list) && list.Count == r.Count);

        return true;
    }
}