using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Defines the contract for managing guild experience and level progression.
/// </summary>
public interface IGuildProgressionService
{
    /// <summary>
    /// Adds experience to the guild and triggers level-ups if the threshold is reached.
    /// </summary>
    /// <param name="guild">The guild entity to update.</param>
    /// <param name="xpGained">Amount of XP awarded.</param>
    void AddXpAndLevelUpIfNeeded(Guild guild, int xpGained);
}