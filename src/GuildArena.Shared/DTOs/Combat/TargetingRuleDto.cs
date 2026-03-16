using GuildArena.Domain.Enums.Targeting;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// A lightweight representation of a targeting rule for the UI.
/// </summary>
public class TargetingRuleDto
{
    public required string RuleId { get; set; }
    public TargetType Type { get; set; }
    public int Count { get; set; }
    public TargetSelectionStrategy Strategy { get; set; }

    /// <summary>
    /// A pre-calculated list of valid combatant IDs that can be selected for this rule.
    /// Calculated by the server to respect Taunt, Stealth, and Racial constraints.
    /// </summary>
    public List<int> ValidTargetIds { get; set; } = new();
}