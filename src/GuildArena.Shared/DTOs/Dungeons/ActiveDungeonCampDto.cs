namespace GuildArena.Shared.DTOs.Dungeons;

/// <summary>
/// Represents the current state of an active dungeon run (the camp screen).
/// </summary>
public class ActiveDungeonCampDto
{
    public required string DungeonId { get; set; }
    public required string DungeonName { get; set; }
    public int CurrentStageIndex { get; set; }
    public bool IsBossNode { get; set; }

    /// <summary>
    /// The heroes selected for the run with their current HP.
    /// </summary>
    public required List<CampHeroDto> Heroes { get; set; }
}
