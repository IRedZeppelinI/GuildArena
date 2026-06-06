namespace GuildArena.Domain.Entities;

/// <summary>
/// Tracks how many times a guild has completed a specific dungeon.
/// </summary>
public class GuildDungeonRecord
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the guild.
    /// </summary>
    public int GuildId { get; set; }

    /// <summary>
    /// Navigation property to the guild.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Static dungeon definition ID.
    /// </summary>
    public string DungeonDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Number of times this dungeon has been fully completed.
    /// </summary>
    public int CompletionCount { get; set; }
}