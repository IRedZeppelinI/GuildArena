using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// A lightweight representation of an ability, containing only the data 
/// required by the UI to render action buttons, tooltips, and handle targeting logic.
/// </summary>
public class AbilitySummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int ActionPointCost { get; set; }
    public int BaseCooldown { get; set; }
    public int HPCost { get; set; }
    public Dictionary<EssenceType, int> Costs { get; set; } = new();

    public int CurrentCooldownTurns { get; set; }

    public List<TargetingRuleDto> TargetingRules { get; set; } = new();
}