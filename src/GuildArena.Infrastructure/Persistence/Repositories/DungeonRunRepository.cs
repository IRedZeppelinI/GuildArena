using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuildArena.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements the dungeon run data access logic.
/// </summary>
public class DungeonRunRepository : IDungeonRunRepository
{
    private readonly GuildArenaDbContext _dbContext;
    private readonly ILogger<DungeonRunRepository> _logger;

    public DungeonRunRepository(GuildArenaDbContext dbContext, ILogger<DungeonRunRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActiveDungeonRun?> GetActiveRunAsync(int guildId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ActiveDungeonRuns
            .Include(r => r.HeroesState)
                .ThenInclude(h => h.Hero)
            .FirstOrDefaultAsync(r => r.GuildId == guildId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.ActiveDungeonRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Created active dungeon run {RunId} for Guild {GuildId} in Dungeon {DungeonId}.",
            run.Id, run.GuildId, run.DungeonDefinitionId);
    }

    /// <inheritdoc />
    public async Task UpdateRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default)
    {
        // EF Core tracks the entity, so just save.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRunAsync(ActiveDungeonRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.ActiveDungeonRuns.Remove(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted active dungeon run {RunId} for Guild {GuildId}.",
            run.Id, run.GuildId);
    }

    /// <inheritdoc />
    public async Task<GuildDungeonRecord> IncrementDungeonRecordAsync(
        int guildId,
        string dungeonDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.GuildDungeonRecords
            .FirstOrDefaultAsync(r => r.GuildId == guildId && r.DungeonDefinitionId == dungeonDefinitionId, cancellationToken);

        if (record == null)
        {
            record = new GuildDungeonRecord
            {
                GuildId = guildId,
                DungeonDefinitionId = dungeonDefinitionId,
                CompletionCount = 1
            };
            _dbContext.GuildDungeonRecords.Add(record);
        }
        else
        {
            record.CompletionCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Guild {GuildId} has now completed Dungeon {DungeonId} {Count} time(s).",
            guildId, dungeonDefinitionId, record.CompletionCount);

        return record;
    }
}