using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions.Repositories;

/// <summary>
/// Handles data access operations related to a player's Guild and their roster of Heroes.
/// </summary>
public interface IGuildRepository
{
    /// <summary>
    /// Retrieves the Guild profile for read-only purposes (e.g., verifying existence).
    /// </summary>
    Task<Guild?> GetGuildByUserIdAsync(string applicationUserId);

    /// <summary>
    /// Retrieves a specific subset of heroes owned by a guild, ensuring they exist and belong to the requester.
    /// Optimized for read-only operations like spawning combatants.
    /// </summary>
    Task<List<Hero>> GetHeroesAsync(int guildId, List<int> heroIds);

    /// <summary>
    /// Retrieves all heroes belonging to a specific guild.
    /// </summary>
    Task<List<Hero>> GetAllHeroesAsync(int guildId);

    /// <summary>
    /// Creates a new Guild and populates it with a starter roster of heroes in a single transaction.
    /// </summary>
    Task CreateWithStarterPackAsync(string applicationUserId, string guildName);

    /// <summary>
    /// Creates a new Guild profile in the database.
    /// </summary>
    Task CreateGuildAsync(Guild guild);

    /// <summary>
    /// Updates an existing Guild profile (e.g., adding wins/losses, MMR, or Gold).
    /// </summary>
    Task UpdateGuildAsync(Guild guild);

    /// <summary>
    /// Retrieves the Guild profile including Heroes and Match History.
    /// Does NOT use AsNoTracking, making it safe for updates (e.g. deductions of gold).
    /// </summary>
    Task<Guild?> GetGuildWithHistoryAsync(string applicationUserId);
}