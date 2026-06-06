namespace GuildArena.Domain.Entities;

/// <summary>
/// Captures the HP of a specific hero during an active dungeon run.
/// </summary>
public class DungeonHeroState
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the active dungeon run.
    /// </summary>
    public int ActiveDungeonRunId { get; set; }

    /// <summary>
    /// Navigation property to the parent dungeon run.
    /// </summary>
    public ActiveDungeonRun? ActiveDungeonRun { get; set; }

    /// <summary>
    /// Foreign key to the hero instance used.
    /// </summary>
    public int HeroId { get; set; }

    /// <summary>
    /// Navigation property to the hero.
    /// </summary>
    public Hero? Hero { get; set; }

    /// <summary>
    /// Current HP of the hero at the start of the next stage.
    /// </summary>
    public int CurrentHP { get; set; }
}