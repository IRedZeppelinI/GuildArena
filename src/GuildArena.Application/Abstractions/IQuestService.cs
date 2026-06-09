using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Manages quest generation, reroll, and progress tracking for a guild.
/// </summary>
public interface IQuestService
{
    /// <summary>
    /// Checks if a new day has begun and, if so, grants up to 3 new daily quests.
    /// Only quests matching the guild's owned heroes/races are selected.
    /// </summary>
    Task GrantDailyQuestsIfNeededAsync(Guild guild);

    /// <summary>
    /// Re‑rolls a specific active (non‑completed) quest, replacing it with a new one.
    /// Can be done once per day.
    /// </summary>
    /// <param name="guild">The guild whose quest should be rerolled.</param>
    /// <param name="activeQuestId">The ID of the quest to replace.</param>
    Task<Result> RerollQuestAsync(Guild guild, int activeQuestId);

    /// <summary>
    /// Processes the end of a match for all active, non‑completed quests.
    /// Advances progress, completes quests, and applies reward gold/XP.
    /// </summary>
    /// <param name="guild">The guild that participated in the match.</param>
    /// <param name="match">The completed match (with participants and heroes used).</param>
    /// <param name="isWinner">Whether the guild won the match.</param>
    Task ProcessMatchEndAsync(Guild guild, Match match, bool isWinner);
}