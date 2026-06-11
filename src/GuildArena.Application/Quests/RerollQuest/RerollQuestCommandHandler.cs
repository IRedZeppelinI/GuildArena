using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Quests.RerollQuest;

/// <summary>
/// Handles the quest re‑roll logic: validates daily limit, replaces quest, saves changes.
/// </summary>
public class RerollQuestCommandHandler : IRequestHandler<RerollQuestCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IQuestService _questService;
    private readonly ILogger<RerollQuestCommandHandler> _logger;

    public RerollQuestCommandHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IQuestService questService,
        ILogger<RerollQuestCommandHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _questService = questService;
        _logger = logger;
    }

    public async Task<Result> Handle(RerollQuestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(new Error(
                "Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var guild = await _guildRepo.GetGuildWithQuestsAsync(userId);
        if (guild == null)
            return Result.Failure(new Error(
                "Guild.NotFound", "No guild found.", ErrorType.NotFound));

        var rerollResult = await _questService.RerollQuestAsync(guild, request.QuestId);
        if (rerollResult.IsFailure)
            return rerollResult; // Propagate error (e.g., already rerolled today)

        await _guildRepo.UpdateGuildAsync(guild);
        return Result.Success();
    }
}