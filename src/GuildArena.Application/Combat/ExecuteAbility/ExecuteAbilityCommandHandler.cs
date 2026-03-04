using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.Targeting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.ExecuteAbility;

public class ExecuteAbilityCommandHandler : IRequestHandler<ExecuteAbilityCommand>
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

    public async Task Handle(ExecuteAbilityCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate User
        var userId = _currentUser.UserId;
        if (userId == null) throw new UnauthorizedAccessException("User not authenticated.");

        // 2. Load Game State
        var gameState = await _combatStateRepo.GetAsync(request.CombatId);
        if (gameState == null) throw new KeyNotFoundException($"Combat {request.CombatId} not found.");

        // 3. Validate Turn
        if (gameState.CurrentPlayerId != userId)
        {
            throw new InvalidOperationException("It is not your turn.");
        }

        // 4. Validate Source Ownership & Existence
        var sourceCombatant = gameState.Combatants.FirstOrDefault(c => c.Id == request.SourceId);
        if (sourceCombatant == null)
        {
            throw new ArgumentException($"Source combatant {request.SourceId} not found in this combat.");
        }

        if (sourceCombatant.OwnerId != userId)
        {
            _logger.LogWarning("User {UserId} tried to control combatant {SourceId} owned by {OwnerId}.",
                userId, request.SourceId, sourceCombatant.OwnerId);
            throw new InvalidOperationException("You do not own this combatant.");
        }

        bool knowsAbility = sourceCombatant.Abilities.Any(a => a.Id == request.AbilityId) ||
                            sourceCombatant.SpecialAbility?.Id == request.AbilityId;
        if (!knowsAbility)
        {
            _logger.LogWarning("Combatant {Source} tried to use unknown ability {AbilityId}.",
                sourceCombatant.Name, request.AbilityId);
            throw new InvalidOperationException
                ($"Combatant {sourceCombatant.Name} does not know ability {request.AbilityId}.");
        }

        // 5. Load Ability Definition
        if (!_abilityRepo.TryGetDefinition(request.AbilityId, out var abilityDef))
        {
            throw new ArgumentException($"Ability {request.AbilityId} does not exist.");
        }

        // 6. Map Inputs to Domain Objects
        var domainTargets = new AbilityTargets { SelectedTargets = request.TargetSelections };

        // 7. Execute via Combat Engine
        var results = _combatEngine.ExecuteAbility(
            gameState,
            abilityDef,
            sourceCombatant,
            domainTargets,
            request.Payment);

        // 8. Retrieve Logs
        var logs = _battleLog.GetAndClearLogs();

        // 9. Check Logic Success
        var rootResult = results.FirstOrDefault();
        if (rootResult == null || !rootResult.IsSuccess)
        {
            _logger.LogWarning("Ability execution failed for {AbilityId}. Source: {Source}",
                request.AbilityId, sourceCombatant.Name);

            // Se falhou (ex: não tem mana), avisamos os clientes com os logs do erro, 
            // mas NÃO gravamos o estado, porque a jogada foi inválida.
            await _notifier.SendBattleLogsAsync(request.CombatId, logs);
            throw new InvalidOperationException("Ability execution failed. Check logs for details.");
        }

        // 10. Persist State
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        // 11. Broadcast Updates via SignalR
        await _notifier.SendBattleLogsAsync(request.CombatId, logs);
        await _notifier.SendGameStateUpdateAsync(request.CombatId, gameState);

        _logger.LogInformation("Ability {Ability} executed successfully by {Source} in Combat {CombatId}.",
            request.AbilityId, sourceCombatant.Name, request.CombatId);
    }
}