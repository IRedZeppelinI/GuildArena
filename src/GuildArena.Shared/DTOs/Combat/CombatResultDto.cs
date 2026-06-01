namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Outcome of a combat session, sent to the client after resolution.
/// </summary>
public class CombatResultDto
{
    /// <summary>
    /// Whether the player who requested the resolution ended as the winner.
    /// </summary>
    public bool IsWinner { get; set; }

    /// <summary>
    /// Total XP gained by the guild.
    /// </summary>
    public int XpGained { get; set; }

    /// <summary>
    /// Total gold gained by the guild.
    /// </summary>
    public int GoldGained { get; set; }

    /// <summary>
    /// The guild's level after processing rewards.
    /// </summary>
    public int NewGuildLevel { get; set; }

    /// <summary>
    /// Indicates if the combat ended through the surrender mechanic.
    /// </summary>
    public bool IsSurrender { get; set; }
}