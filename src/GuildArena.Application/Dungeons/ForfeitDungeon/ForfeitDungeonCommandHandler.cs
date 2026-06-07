using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Dungeons.ForfeitDungeon;

/// <summary>
/// Deletes the active dungeon run for the current user's guild.
/// </summary>
public class ForfeitDungeonCommandHandler : IRequestHandler<ForfeitDungeonCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IDungeonRunRepository _dungeonRunRepo;
    private readonly ILogger<ForfeitDungeonCommandHandler> _logger;

    public ForfeitDungeonCommandHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IDungeonRunRepository dungeonRunRepo,
        ILogger<ForfeitDungeonCommandHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _dungeonRunRepo = dungeonRunRepo;
        _logger = logger;
    }

    public async Task<Result> Handle(ForfeitDungeonCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(new Error("Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var guild = await _guildRepo.GetGuildByUserIdAsync(userId);
        if (guild == null)
            return Result.Failure(new Error("Dungeon.NoGuild", "No guild found.", ErrorType.NotFound));

        var activeRun = await _dungeonRunRepo.GetActiveRunAsync(guild.Id, cancellationToken);
        if (activeRun == null)
            return Result.Failure(new Error("Dungeon.NoActiveRun", "No active dungeon run.", ErrorType.NotFound));

        await _dungeonRunRepo.DeleteRunAsync(activeRun, cancellationToken);

        _logger.LogInformation("Guild {GuildId} forfeited dungeon run {DungeonId}.", guild.Id, activeRun.DungeonDefinitionId);
        return Result.Success();
    }
}