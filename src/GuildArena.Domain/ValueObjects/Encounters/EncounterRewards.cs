namespace GuildArena.Domain.ValueObjects.Encounters;

/// <summary>
/// Defines the base rewards granted upon completing an encounter.
/// </summary>
public class EncounterRewards
{
    /// <summary>
    /// Base experience points awarded to the player's guild.
    /// </summary>
    public int BaseGuildXp { get; set; }

    /// <summary>
    /// Base gold awarded to the player's guild.
    /// </summary>
    public int BaseGold { get; set; }
}