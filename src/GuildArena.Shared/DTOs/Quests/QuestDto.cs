namespace GuildArena.Shared.DTOs.Quests;

/// <summary>
/// Represents the client-facing state of an active quest.
/// </summary>
public class QuestDto
{
    /// <summary>
    /// The unique ID of the active quest record (from <see cref="ActiveQuest.Id"/>).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The static definition ID of the quest.
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Quest display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Flavour description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gold reward on completion.
    /// </summary>
    public int RewardGold { get; set; }

    /// <summary>
    /// Guild XP reward on completion.
    /// </summary>
    public int RewardXP { get; set; }

    /// <summary>
    /// Current progress (e.g., 2 out of 5).
    /// </summary>
    public int CurrentProgress { get; set; }

    /// <summary>
    /// The target value required to complete.
    /// </summary>
    public int TargetValue { get; set; }

    /// <summary>
    /// Whether the quest has been completed (rewards may or may not have been claimed).
    /// </summary>
    public bool IsCompleted { get; set; }
}