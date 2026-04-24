using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Resources;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.ExchangeEssence;

/// <summary>
/// Handles the essence exchange logic. Validates turn ownership, resource availability, 
/// performs the exchange, and broadcasts the updated state.
/// </summary>
public class ExchangeEssenceCommandHandler : IRequestHandler<ExchangeEssenceCommand, Result>
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

    /// <summary>
    /// Processes the exchange essence command.
    /// </summary>
    /// <param name="request">The command containing the exchange details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success. Returns a failure result if the exchange rule is violated, 
    /// funds are insufficient, or it is not the user's turn.
    /// </returns>
    public async Task<Result> Handle(ExchangeEssenceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
        {
            return Result.Failure(new Error(
                "Auth.Unauthorized",
                "User not authenticated.",
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

        var totalSpent = request.EssenceToSpend.Values.Sum();
        if (totalSpent != 2)
        {
            return Result.Failure(new Error(
                "Exchange.InvalidAmount",
                $"Exchange requires exactly 2 essence. Provided: {totalSpent}.",
                ErrorType.Validation));
        }

        var player = gameState.Players.First(p => p.PlayerId == userId);
        var costsToValidate = request.EssenceToSpend
            .Select(kvp => new EssenceAmount { Type = kvp.Key, Amount = kvp.Value })
            .ToList();

        if (!_essenceService.HasEnoughEssence(player, costsToValidate))
        {
            _logger.LogWarning("Player {PlayerId} attempted to exchange essence they do not own.", userId);

            return Result.Failure(new Error(
                "Exchange.InsufficientEssence",
                "You do not have enough essence to perform this exchange.",
                ErrorType.Validation));
        }

        _essenceService.ConsumeEssence(player, request.EssenceToSpend);
        _essenceService.AddEssence(player, request.EssenceToGain, 1);

        _battleLog.Log($"{player.Name} exchanged essence for {request.EssenceToGain} Essence.");

        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        var logs = _battleLog.GetAndClearLogs();
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation("Player {PlayerId} successfully exchanged essence in combat {CombatId}.",
            userId, request.CombatId);

        return Result.Success();
    }
}