namespace GuildArena.Domain.Enums.UnlockHero;

/// <summary>
/// Represents the acquisition state of a hero from the perspective of a specific guild.
/// </summary>
public enum HeroStatus
{
    /// <summary>
    /// The hero is already owned by the guild.
    /// </summary>
    Owned,

    /// <summary>
    /// The hero is not owned and one or more unlock conditions are not yet fulfilled.
    /// </summary>
    Locked,

    /// <summary>
    /// The hero is not owned, but all unlock conditions are satisfied – 
    /// only the gold cost remains to be paid.
    /// </summary>
    Available
}