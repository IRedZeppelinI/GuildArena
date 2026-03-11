using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.ValueObjects.Resources;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.ExchangeEssence;

/// <summary>
/// Handles the essence exchange logic. Validates turn ownership, resource availability, 
/// performs the exchange, and broadcasts the updated state.
/// </summary>
public class ExchangeEssenceCommandHandler : IRequestHandler<ExchangeEssenceCommand>
{
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IEssenceService _essenceService;
    private readonly ICombatNotifier _notifier;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<ExchangeEssenceCommandHandler> _logger;

    public ExchangeEssenceCommandHandler(
        ICombatStateRepository combatStateRepo,
        ICurrentUserService currentUser,
        IEssenceService essenceService,
        ICombatNotifier notifier,
        IBattleLogService battleLog,
        ILogger<ExchangeEssenceCommandHandler> logger)
    {
        _combatStateRepo = combatStateRepo;
        _currentUser = currentUser;
        _essenceService = essenceService;
        _notifier = notifier;
        _battleLog = battleLog;
        _logger = logger;
    }

    public async Task Handle(ExchangeEssenceCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate User & State
        var userId = _currentUser.UserId;
        if (userId == null)
            throw new UnauthorizedAccessException("User not authenticated.");

        var gameState = await _combatStateRepo.GetAsync(request.CombatId);
        if (gameState == null)
            throw new KeyNotFoundException($"Combat {request.CombatId} not found.");

        if (gameState.CurrentPlayerId != userId)
            throw new InvalidOperationException("It is not your turn.");

        // 2. Validate Exchange Rule (Exactly 2 for 1)
        var totalSpent = request.EssenceToSpend.Values.Sum();
        if (totalSpent != 2)
            throw new ArgumentException($"Exchange requires exactly 2 essence. Provided: {totalSpent}.");

        var player = gameState.Players.First(p => p.PlayerId == userId);

        // 3. Convert dictionary to EssenceAmount list for the service check
        var costsToValidate = request.EssenceToSpend
            .Select(kvp => new EssenceAmount { Type = kvp.Key, Amount = kvp.Value })
            .ToList();

        // 4. Validate if player actually owns the essence
        if (!_essenceService.HasEnoughEssence(player, costsToValidate))
        {
            _logger.LogWarning("Player {PlayerId} attempted to exchange essence they do not own.", userId);
            throw new InvalidOperationException("You do not have enough essence to perform this exchange.");
        }

        // 5. Execute Exchange
        _essenceService.ConsumeEssence(player, request.EssenceToSpend);
        _essenceService.AddEssence(player, request.EssenceToGain, 1);

        // 6. Provide specific Battle Log feedback
        // The AddEssence method already logs the gain, but we can log the explicit exchange action
        _battleLog.Log($"{player.Name} exchanged essence for {request.EssenceToGain} Essence.");

        // 7. Save & Broadcast
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        var logs = _battleLog.GetAndClearLogs();
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation("Player {PlayerId} successfully exchanged essence in combat {CombatId}.",
            userId, request.CombatId);
    }
}