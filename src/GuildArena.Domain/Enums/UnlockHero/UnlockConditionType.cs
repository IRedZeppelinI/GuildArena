namespace GuildArena.Domain.Enums.UnlockHero;

/// <summary>
/// Defines the types of conditions a player must meet to unlock a hero.
/// </summary>
public enum UnlockConditionType
{
    /// <summary>
    /// The player's guild must be at a minimum level.
    /// </summary>
    GuildLevel,

    /// <summary>
    /// The player must have played a minimum number of matches
    /// using at least one hero of a specific race.
    /// </summary>
    MatchesPlayedWithRace,

    /// <summary>
    /// The player must have won a minimum number of matches
    /// using at least one hero of a specific race.
    /// </summary>
    MatchesWonWithRace
}