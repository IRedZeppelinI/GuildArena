using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Targeting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.ExecuteAbility;

/// <summary>
/// Handles the execution of a combat ability, validating game rules, costs, and state.
/// </summary>
public class ExecuteAbilityCommandHandler : IRequestHandler<ExecuteAbilityCommand, Result>
{
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly ICombatEngine _combatEngine;
    private readonly IBattleLogService _battleLog;
    private readonly ICombatNotifier _notifier;
    private readonly ILogger<ExecuteAbilityCommandHandler> _logger;

    public ExecuteAbilityCommandHandler(
        ICombatStateRepository combatStateRepo,
        ICurrentUserService currentUser,
        IAbilityDefinitionRepository abilityRepo,
        ICombatEngine combatEngine,
        IBattleLogService battleLog,
        ICombatNotifier notifier,
        ILogger<ExecuteAbilityCommandHandler> logger)
    {
        _combatStateRepo = combatStateRepo;
        _currentUser = currentUser;
        _abilityRepo = abilityRepo;
        _combatEngine = combatEngine;
        _battleLog = battleLog;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// Processes the execute ability command.
    /// </summary>
    /// <param name="request">The command containing the ability, source, targets, and payment details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success. Returns a failure result if validation fails 
    /// (e.g., insufficient essence, invalid turn, stunned state).
    /// </returns>
    public async Task<Result> Handle(
        ExecuteAbilityCommand request,
        CancellationToken cancellationToken)
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

        var sourceCombatant = gameState.Combatants.FirstOrDefault(c => c.Id == request.SourceId);
        if (sourceCombatant == null)
        {
            return Result.Failure(new Error(
                "Combat.SourceNotFound",
                $"Source combatant {request.SourceId} not found in this combat.",
                ErrorType.Validation));
        }

        if (sourceCombatant.OwnerId != userId)
        {
            _logger.LogWarning(
                "User {UserId} tried to control combatant {SourceId} owned by {OwnerId}.",
                userId, request.SourceId, sourceCombatant.OwnerId);

            return Result.Failure(new Error(
                "Combat.NotOwner",
                "You do not own this combatant.",
                ErrorType.Forbidden));
        }

        bool knowsAbility = sourceCombatant.Abilities.Any(a => a.Id == request.AbilityId) ||
                            sourceCombatant.SpecialAbility?.Id == request.AbilityId;

        if (!knowsAbility)
        {
            return Result.Failure(new Error(
                "Combat.UnknownAbility",
                $"Combatant {sourceCombatant.Name} does not know ability {request.AbilityId}.",
                ErrorType.Validation));
        }

        if (!_abilityRepo.TryGetDefinition(request.AbilityId, out var abilityDef))
        {
            return Result.Failure(new Error(
                "Combat.InvalidAbility",
                $"Ability definition {request.AbilityId} does not exist.",
                ErrorType.NotFound));
        }

        var domainTargets = new AbilityTargets { SelectedTargets = request.TargetSelections };

        var results = _combatEngine.ExecuteAbility(
            gameState,
            abilityDef,
            sourceCombatant,
            domainTargets,
            request.Payment);

        var logs = _battleLog.GetAndClearLogs();
        var rootResult = results.FirstOrDefault();

        if (rootResult == null || !rootResult.IsSuccess)
        {
            _logger.LogWarning(
                "Ability execution failed for {AbilityId}. Source: {Source}",
                request.AbilityId, sourceCombatant.Name);

            // Notify clients about the failure logs, but DO NOT save the state
            await _notifier.SendBattleLogsAsync(request.CombatId, logs);

            return Result.Failure(new Error(
                "Combat.ExecutionFailed",
                "Ability execution failed due to game logic (e.g., insufficient essence, stunned). Check battle logs.",
                ErrorType.Failure));
        }

        await _combatStateRepo.SaveAsync(request.CombatId, gameState);
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation(
            "Ability {Ability} executed successfully by {Source} in Combat {CombatId}.",
            request.AbilityId, sourceCombatant.Name, request.CombatId);

        return Result.Success();
    }
}