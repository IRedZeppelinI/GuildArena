using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// A lightweight representation of an ability, containing only the data 
/// required by the UI to render action buttons and tooltips.
/// </summary>
public class AbilitySummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int ActionPointCost { get; set; }
    public int BaseCooldown { get; set; }
    public int CurrentCooldownTurns { get; set; }

    /// <summary>
    /// Key-value pairs of required essence (e.g., Vigor: 2, Neutral: 1).
    /// </summary>
    public Dictionary<EssenceType, int> Costs { get; set; } = new();

    public int HPCost { get; set; }
}