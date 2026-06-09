using GuildArena.Domain.Enums.Quests;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines a static quest that can be assigned to a guild.
/// Loaded from JSON data files.
/// </summary>
public class QuestDefinition
{
    /// <summary>
    /// Unique identifier (e.g. "QUEST_DAILY_WIN_1").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown to the player.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Flavour text or instructions.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount of gold awarded upon completion.
    /// </summary>
    public int RewardGold { get; set; }

    /// <summary>
    /// Amount of guild XP awarded upon completion.
    /// </summary>
    public int RewardXP { get; set; }

    /// <summary>
    /// How the quest progress is measured.
    /// </summary>
    public QuestRequirementType RequirementType { get; set; }

    /// <summary>
    /// Total number of matches/events required to complete the quest.
    /// </summary>
    public int TargetValue { get; set; } = 1;

    /// <summary>
    /// If <see cref="RequirementType"/> is <see cref="QuestRequirementType.PlayWithRace"/>,
    /// this specifies the required race ID (e.g. "RACE_HUMAN").
    /// </summary>
    public string? RequiredRaceId { get; set; }

    /// <summary>
    /// If <see cref="RequirementType"/> is <see cref="QuestRequirementType.PlayWithHero"/>,
    /// this specifies the required hero definition ID (e.g. "HERO_GARRET").
    /// </summary>
    public string? RequiredHeroDefinitionId { get; set; }
}