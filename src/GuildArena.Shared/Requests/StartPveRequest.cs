namespace GuildArena.Shared.Requests;

/// <summary>
/// Data contract for initiating a PvE combat session against a specific encounter.
/// </summary>
public class StartPveRequest
{
    /// <summary>
    /// The unique ID of the encounter configuration (e.g. "ENC_TUTORIAL_01").
    /// </summary>
    public required string EncounterId { get; set; }

    /// <summary>
    /// The unique IDs (Database IDs) of the specific hero instances the player wants to use.    
    /// </summary>
    public List<int> HeroInstanceIds { get; set; } = new();
}