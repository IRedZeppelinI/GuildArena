using GuildArena.Domain.Enums.UnlockHero;

namespace GuildArena.Domain.ValueObjects.UnlockHero;

/// <summary>
/// Represents a single requirement that must be satisfied to unlock a hero.
/// Only the fields relevant to the <see cref="Type"/> are evaluated.
/// </summary>
public class UnlockHeroCondition
{
    /// <summary>
    /// The type of condition (discriminator for evaluation logic).
    /// </summary>
    public UnlockConditionType Type { get; set; }

    /// <summary>
    /// Human-readable description shown in the UI (e.g., "Guild reaches Level 3.").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // ---- Fields for GuildLevel ----

    /// <summary>
    /// Minimum guild level required. Used when <see cref="Type"/> is <see cref="UnlockConditionType.GuildLevel"/>.
    /// </summary>
    public int? MinLevel { get; set; }

    // ---- Fields for MatchesPlayedWithRace / MatchesWonWithRace ----

    /// <summary>
    /// The race ID that must be present in the player's team.
    /// Used when <see cref="Type"/> involves race-based match conditions.
    /// </summary>
    public string? RaceId { get; set; }

    /// <summary>
    /// Minimum number of matches (played or won) required.
    /// Used when <see cref="Type"/> involves match-count conditions.
    /// </summary>
    public int? MinCount { get; set; }
}