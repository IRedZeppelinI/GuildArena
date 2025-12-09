namespace GuildArena.Shared.Requests;

/// <summary>
/// Data contract for initiating a PvE combat session.
/// </summary>
public class StartPveRequest
{
    /// <summary>
    /// The ID of the player starting the combat.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The list of Hero Definition IDs to bring to battle (e.g. ["HERO_GARRET", "HERO_MAGE"]).
    /// If empty, the server may provide a default team for testing.
    /// </summary>
    public List<string> HeroIds { get; set; } = new();
}