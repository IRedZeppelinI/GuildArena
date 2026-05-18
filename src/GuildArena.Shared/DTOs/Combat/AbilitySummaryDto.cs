using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// A lightweight representation of an ability for the UI.
/// </summary>
public class AbilitySummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ActionPointCost { get; set; }
    public int BaseCooldown { get; set; }
    public int HPCost { get; set; }
    public Dictionary<EssenceType, int> Costs { get; set; } = new();

    public int CurrentCooldownTurns { get; set; }

    /// <summary>
    /// Pre-calculated by the server: true if the player has enough AP, HP, and Essence to cast this.
    /// </summary>
    public bool IsAffordable { get; set; }

    public List<TargetingRuleDto> TargetingRules { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public List<AbilityEffectSummaryDto> Effects { get; set; } = new();
}