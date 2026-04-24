using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Handles the request to end the current turn.
/// Validates turn ownership, advances the combat state, broadcasts updates, 
/// and orchestrates the AI's turn if applicable.
/// </summary>
public class EndTurnCommandHandler : IRequestHandler<EndTurnCommand, Result>
{
    private readonly ITurnManagerService _turnManagerService;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICombatNotifier _notifier;
    private readonly IBattleLogService _battleLog;
    private readonly IAiTurnQueue _aiQueue;
    private readonly ILogger<EndTurnCommandHandler> _logger;

    public EndTurnCommandHandler(
        ITurnManagerService turnManagerService,
        ICombatStateRepository combatStateRepo,
        ICurrentUserService currentUserService,
        ICombatNotifier notifier,
        IBattleLogService battleLog,
        IAiTurnQueue aiQueue,
        ILogger<EndTurnCommandHandler> logger)
    {
        _turnManagerService = turnManagerService;
        _combatStateRepo = combatStateRepo;
        _currentUserService = currentUserService;
        _notifier = notifier;
        _battleLog = battleLog;
        _aiQueue = aiQueue;
        _logger = logger;
    }

    /// <summary>
    /// Processes the end turn command, updates the combat state, and handles AI transitions.
    /// </summary>
    /// <param name="request">The command containing the combat ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success. Returns a failure result if the user is unauthenticated, 
    /// the combat session does not exist, or it is not the user's turn.
    /// </returns>
    public async Task<Result> Handle(EndTurnCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            return Result.Failure(new Error(
                "Auth.Unauthorized",
                "User is not authenticated.",
                ErrorType.Unauthorized));
        }

        var gameState = await _combatStateRepo.GetAsync(request.CombatId);
        if (gameState == null)
        {
            return Result.Failure(new Error(
                "Combat.NotFound",
                $"Combat {request.CombatId} not found.",
                ErrorType.NotFound));
        }

        if (gameState.CurrentPlayerId != userId)
        {
            return Result.Failure(new Error(
                "Combat.NotYourTurn",
                "It is not your turn.",
                ErrorType.Forbidden));
        }

        _logger.LogInformation(
            "User {UserId} ending turn {TurnNumber} in Combat {CombatId}.",
            userId, gameState.CurrentTurnNumber, request.CombatId);

        _turnManagerService.AdvanceTurn(gameState);

        var nextPlayer = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
        _battleLog.Log($"--- It is now {nextPlayer.Name}'s turn ---");

        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        var logs = _battleLog.GetAndClearLogs();
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        if (nextPlayer.Type == Domain.Enums.Combat.CombatPlayerType.AI)
        {
            var aiRequest = new AiTurnRequest(request.CombatId, nextPlayer.PlayerId);
            await _aiQueue.EnqueueAsync(aiRequest, cancellationToken);
        }

        return Result.Success();
    }
}