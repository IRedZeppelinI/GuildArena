namespace GuildArena.Domain.ValueObjects.Targeting;

/// <summary>
/// A data object that holds the targets selected by the UI,
/// mapped by their corresponding TargetingRuleId.
/// </summary>
public class AbilityTargets
{
    /// <summary>
    /// A map of selected targets.
    /// Key: The "RuleId" from the TargetingRule.
    /// Value: A list of Combatant IDs the player selected for that specific rule.
    /// </summary>
    public Dictionary<string, List<int>> SelectedTargets { get; set; } = new();
}