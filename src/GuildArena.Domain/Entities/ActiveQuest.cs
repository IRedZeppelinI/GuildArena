namespace GuildArena.Domain.Entities;

/// <summary>
/// Represents an active quest assigned to a guild, tracking its progress.
/// </summary>
public class ActiveQuest
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the owning guild.
    /// </summary>
    public int GuildId { get; set; }

    /// <summary>
    /// Navigation property to the guild.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// The static definition ID of the quest (e.g. "QUEST_DAILY_WIN_1").
    /// </summary>
    public string QuestDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Current progress towards the target value (e.g. matches played).
    /// </summary>
    public int CurrentProgress { get; set; }

    /// <summary>
    /// Indicates whether the quest has been completed (and rewards claimed).
    /// </summary>
    public bool IsCompleted { get; set; }
}