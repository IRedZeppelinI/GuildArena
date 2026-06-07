namespace GuildArena.Shared.DTOs.Dungeons;

/// <summary>
/// Lightweight representation of a dungeon shown in the selection list.
/// </summary>
public class DungeonSummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int RequiredGuildLevel { get; set; }
}