using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Handles the request to end the current turn.
/// Validates ownership of the turn and orchestrates the transition to the next player.
/// </summary>
public class EndTurnCommandHandler : IRequestHandler<EndTurnCommand>
{
    private readonly ITurnManagerService _turnManagerService;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICombatNotifier _notifier; 
    private readonly IBattleLogService _battleLog; 
    private readonly ILogger<EndTurnCommandHandler> _logger;

    public EndTurnCommandHandler(
        ITurnManagerService turnManagerService,
        ICombatStateRepository combatStateRepo,
        ICurrentUserService currentUserService,
        ICombatNotifier notifier, 
        IBattleLogService battleLog, 
        ILogger<EndTurnCommandHandler> logger)
    {
        _turnManagerService = turnManagerService;
        _combatStateRepo = combatStateRepo;
        _currentUserService = currentUserService;
        _notifier = notifier;
        _battleLog = battleLog;
        _logger = logger;
    }

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

        // 1. Avançar o turno (isto dispara triggers como Venenos e gera Essence)
        _turnManagerService.AdvanceTurn(gameState);

        // 2. Persistir
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        // 3. Recolher os logs gerados na passagem de turno (ex: "Korg ganhou 4 Essence")
        var logs = _battleLog.GetAndClearLogs();

        // 4. Avisar os clientes (SignalR)
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation("Combat {CombatId} advanced to Player {NextPlayer}.",
            request.CombatId, gameState.CurrentPlayerId);
    }
}