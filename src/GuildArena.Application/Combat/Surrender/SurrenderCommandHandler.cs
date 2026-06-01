using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.Surrender;

/// <summary>
/// Handles the surrender request, updates combat status, triggers resolution and notifies clients.
/// </summary>
public class SurrenderCommandHandler : IRequestHandler<SurrenderCommand, Result>
{
    private readonly ICombatStateRepository _combatRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICombatResolutionService _resolutionService;
    private readonly ICombatNotifier _notifier;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<SurrenderCommandHandler> _logger;

    public SurrenderCommandHandler(
        ICombatStateRepository combatRepo,
        ICurrentUserService currentUser,
        ICombatResolutionService resolutionService,
        ICombatNotifier notifier,
        IBattleLogService battleLog,
        ILogger<SurrenderCommandHandler> logger)
    {
        _combatRepo = combatRepo;
        _currentUser = currentUser;
        _resolutionService = resolutionService;
        _notifier = notifier;
        _battleLog = battleLog;
        _logger = logger;
    }

    public async Task<Result> Handle(SurrenderCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(new Error("Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var gameState = await _combatRepo.GetAsync(request.CombatId);
        if (gameState == null)
            return Result.Failure(new Error("Combat.NotFound", $"Combat {request.CombatId} not found.", ErrorType.NotFound));

        var player = gameState.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null)
            return Result.Failure(new Error("Combat.NotParticipant", "You are not in this combat.", ErrorType.Forbidden));

        // Mark surrender status dynamically
        int seatId = player.PlayerId;
        if (gameState.Players.Count > 0 && seatId == gameState.Players[0].PlayerId)
            gameState.Status = CombatStatus.Player2Won;
        else
            gameState.Status = CombatStatus.Player1Won;

        _battleLog.Log($"{player.Name} has surrendered!");

        // Save state so the notification reflects the new status
        await _combatRepo.SaveAsync(request.CombatId, gameState);
        var logs = _battleLog.GetAndClearLogs();
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        // Resolve combat (rewards, history, cleanup)
        var resultDto = await _resolutionService.ResolveCombatAsync(
            request.CombatId, gameState, userId, isSurrender: true, cancellationToken);

        await _notifier.SendCombatEndedAsync(request.CombatId, resultDto);

        _logger.LogInformation("Player {UserId} surrendered in combat {CombatId}.", userId, request.CombatId);
        return Result.Success();
    }
}