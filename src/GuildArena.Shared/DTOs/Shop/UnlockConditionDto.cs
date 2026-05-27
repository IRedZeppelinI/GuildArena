using GuildArena.Domain.Enums.UnlockHero;

namespace GuildArena.Shared.DTOs.Shop;

/// <summary>
/// Describes a single unlock condition for a hero, including the current progress of the guild.
/// </summary>
public class UnlockConditionDto
{
    /// <summary>
    /// The type of condition (GuildLevel, MatchesPlayedWithRace, etc.).
    /// </summary>
    public UnlockConditionType Type { get; set; }

    /// <summary>
    /// The required threshold for this condition.
    /// </summary>
    public int RequiredValue { get; set; }

    /// <summary>
    /// The current value achieved by the guild (used to display progress).
    /// </summary>
    public int CurrentValue { get; set; }

    /// <summary>
    /// Human-readable description of the condition, e.g., "Guild reaches Level 3.".
    /// </summary>
    public string Description { get; set; } = string.Empty;
}