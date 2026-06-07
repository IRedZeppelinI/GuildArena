namespace GuildArena.Shared.DTOs.Dungeons;

/// <summary>
/// Snapshot of a hero inside a dungeon run.
/// </summary>
public class CampHeroDto
{
    public int HeroId { get; set; }
    public required string Name { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
}