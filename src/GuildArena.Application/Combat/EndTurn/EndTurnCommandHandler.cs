using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.AI;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Handles the request to end the current turn.
/// Validates turn ownership, advances the combat state, broadcasts updates via SignalR, 
/// and orchestrates the AI's turn if the next player is computer-controlled.
/// </summary>
public class EndTurnCommandHandler : IRequestHandler<EndTurnCommand>
{
    private readonly ITurnManagerService _turnManagerService;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICombatNotifier _notifier;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<EndTurnCommandHandler> _logger;
    private readonly IAiTurnQueue _aiQueue;

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
        _logger = logger;
        _aiQueue = aiQueue;
    }

    /// <summary>
    /// Processes the end turn command, updates the combat state, and handles AI transitions.
    /// </summary>
    /// <param name="request">The command containing the combat ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authenticated.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the combat session does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the user attempts to end a turn they do not own.</exception>
    public async Task Handle(EndTurnCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        var gameState = await _combatStateRepo.GetAsync(request.CombatId);
        if (gameState == null)
            throw new KeyNotFoundException($"Combat {request.CombatId} not found.");

        if (gameState.CurrentPlayerId != userId)
            throw new InvalidOperationException("It is not your turn.");

        _logger.LogInformation("User {UserId} ending turn {TurnNumber} in Combat {CombatId}.",
            userId, gameState.CurrentTurnNumber, request.CombatId);

        // 1. Advance the turn (this processes DoTs, cooldowns, and generates new essence)
        _turnManagerService.AdvanceTurn(gameState);

        // 2. Identify the next player to log it properly for the UI
        var nextPlayer = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
        _battleLog.Log($"--- It is now {nextPlayer.Name}'s turn ---");

        // 3. Persist the new state
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        // 4. Retrieve and broadcast logs/state via SignalR
        var logs = _battleLog.GetAndClearLogs();
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation("Combat {CombatId} advanced to Player {NextPlayer}.",
            request.CombatId, gameState.CurrentPlayerId);

        // 5. AI Trigger: If the next player is AI, enqueue the turn request.
        if (nextPlayer.Type == CombatPlayerType.AI)
        {
            var aiRequest = new AiTurnRequest(request.CombatId, nextPlayer.PlayerId);

            // We send the ticket to the channel. The BackgroundService will pick it up and process it safely.
            await _aiQueue.EnqueueAsync(aiRequest, cancellationToken);
        }
    }
}