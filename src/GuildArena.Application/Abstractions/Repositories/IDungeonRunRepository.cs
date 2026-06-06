using GuildArena.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace GuildArena.Application.Abstractions.Repositories;

/// <summary>
/// Defines data access operations for dungeon run state.
/// </summary>
public interface IDungeonRunRepository
{
    /// <summary>
    /// Retrieves the active dungeon run for the specified guild, if any.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ActiveDungeonRun?> GetActiveRunAsync(int guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new dungeon run.
    /// </summary>
    /// <param name="run">The run to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing dungeon run (e.g., stage progress, hero HP).
    /// </summary>
    /// <param name="run">The run to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a dungeon run (end/abandon).
    /// </summary>
    /// <param name="run">The run to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments (or creates) a dungeon completion record for the given guild and dungeon.
    /// Returns the updated record.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="dungeonDefinitionId">The dungeon definition ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GuildDungeonRecord> IncrementDungeonRecordAsync(
        int guildId,
        string dungeonDefinitionId,
        CancellationToken cancellationToken = default);
}