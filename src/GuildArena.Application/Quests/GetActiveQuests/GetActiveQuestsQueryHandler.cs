using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Quests;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Quests.GetActiveQuests;

/// <summary>
/// Handles the retrieval of active quests, automatically granting daily quests if necessary.
/// </summary>
public class GetActiveQuestsQueryHandler : IRequestHandler<GetActiveQuestsQuery, Result<List<QuestDto>>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IQuestDefinitionRepository _questDefRepo;
    private readonly IQuestService _questService;
    private readonly ILogger<GetActiveQuestsQueryHandler> _logger;

    public GetActiveQuestsQueryHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IQuestDefinitionRepository questDefRepo,
        IQuestService questService,
        ILogger<GetActiveQuestsQueryHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _questDefRepo = questDefRepo;
        _questService = questService;
        _logger = logger;
    }

    public async Task<Result<List<QuestDto>>> Handle(GetActiveQuestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<List<QuestDto>>(new Error(
                "Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var guild = await _guildRepo.GetGuildWithHistoryAsync(userId);
        if (guild == null)
            return Result.Failure<List<QuestDto>>(new Error(
                "Guild.NotFound", "No guild found.", ErrorType.NotFound));

        // Grant daily quests if needed – this modifies the guild in-memory
        await _questService.GrantDailyQuestsIfNeededAsync(guild);

        // Persist any changes (e.g., new quests added, timestamps updated)
        await _guildRepo.UpdateGuildAsync(guild);

        // Map to DTOs, merging definition data
        var allDefs = _questDefRepo.GetAllDefinitions();
        var dtos = guild.ActiveQuests
            .Select(q =>
            {
                allDefs.TryGetValue(q.QuestDefinitionId, out var def);
                return new QuestDto
                {
                    Id = q.Id,
                    DefinitionId = q.QuestDefinitionId,
                    Name = def?.Name ?? "Unknown Quest",
                    Description = def?.Description ?? "",
                    RewardGold = def?.RewardGold ?? 0,
                    RewardXP = def?.RewardXP ?? 0,
                    CurrentProgress = q.CurrentProgress,
                    TargetValue = def?.TargetValue ?? 1,
                    IsCompleted = q.IsCompleted
                };
            })
            .ToList();

        return dtos;
    }
}