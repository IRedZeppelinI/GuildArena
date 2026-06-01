namespace GuildArena.Application.Combat.Resolution;

/// <summary>
/// Represents the outcome of applying match rewards.
/// </summary>
public class MatchRewardResult
{
    /// <summary>
    /// Total experience awarded to the guild.
    /// </summary>
    public int XpEarned { get; set; }

    /// <summary>
    /// Total gold awarded to the guild.
    /// </summary>
    public int GoldEarned { get; set; }

    /// <summary>
    /// Indicates whether the guild achieved a new level during this reward application.
    /// </summary>
    public bool LeveledUp { get; set; }
}