using GuildArena.Domain.Enums.Targeting;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// A lightweight representation of a targeting rule for the UI, 
/// instructing the client on how many and what kind of targets to select.
/// </summary>
public class TargetingRuleDto
{
    public required string RuleId { get; set; }
    public TargetType Type { get; set; }
    public int Count { get; set; }
    public TargetSelectionStrategy Strategy { get; set; }
}