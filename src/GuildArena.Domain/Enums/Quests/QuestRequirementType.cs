namespace GuildArena.Domain.Enums.Quests;

/// <summary>
/// Defines how a quest's progress is measured.
/// </summary>
public enum QuestRequirementType
{
    /// <summary>
    /// Win any match (Encounter, Dungeon, PvP).
    /// </summary>
    WinMatch,

    /// <summary>
    /// Play any match (win or lose).
    /// </summary>
    PlayMatch,

    /// <summary>
    /// Play a match using at least one hero of a specific race.
    /// <see cref="QuestDefinition.RequiredRaceId"/> specifies the race.
    /// </summary>
    PlayWithRace,

    /// <summary>
    /// Play a match using a specific hero definition.
    /// <see cref="QuestDefinition.RequiredHeroDefinitionId"/> identifies the hero.
    /// </summary>
    PlayWithHero
}