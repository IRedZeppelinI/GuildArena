namespace GuildArena.Shared.Requests;

/// <summary>
/// Request payload to start a new dungeon run.
/// </summary>
public class StartDungeonRequest
{
    public required string DungeonId { get; init; }
    public required List<int> HeroInstanceIds { get; init; }
}