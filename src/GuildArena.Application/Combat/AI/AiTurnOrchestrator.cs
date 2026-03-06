using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.AI;

public class AiTurnOrchestrator : IAiTurnOrchestrator
{
    private readonly ICombatStateRepository _combatRepo;
    private readonly IAiBehavior _behavior;
    private readonly ICombatEngine _engine;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly ITurnManagerService _turnManager;
    private readonly ICombatNotifier _notifier;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<AiTurnOrchestrator> _logger;

    public AiTurnOrchestrator(
        ICombatStateRepository combatRepo,
        IAiBehavior behavior,
        ICombatEngine engine,
        IAbilityDefinitionRepository abilityRepo,
        ITurnManagerService turnManager,
        ICombatNotifier notifier,
        IBattleLogService battleLog,
        ILogger<AiTurnOrchestrator> logger)
    {
        _combatRepo = combatRepo;
        _behavior = behavior;
        _engine = engine;
        _abilityRepo = abilityRepo;
        _turnManager = turnManager;
        _notifier = notifier;
        _battleLog = battleLog;
        _logger = logger;
    }

    public async Task PlayTurnAsync(string combatId, int aiPlayerId)
    {
        _logger.LogInformation("AI Orchestrator taking control for Player {AiPlayerId} in Combat {CombatId}.", aiPlayerId, combatId);

        try
        {
            // Aumentado para 3000ms. Garante que o Blazor (Frontend) tem tempo de sobra 
            // para estabelecer a ligação SignalR antes da IA começar a falar.
            await Task.Delay(3000);

            while (true)
            {
                var gameState = await _combatRepo.GetAsync(combatId);
                if (gameState == null || gameState.CurrentPlayerId != aiPlayerId)
                {
                    _logger.LogWarning("AI Turn cancelled. Combat missing or not AI's turn.");
                    break;
                }

                var intent = _behavior.DecideNextAction(gameState, aiPlayerId);

                if (intent == null)
                {
                    _logger.LogInformation("AI Player {Id} finished their turn.", aiPlayerId);

                    _turnManager.AdvanceTurn(gameState);

                    // NOVO: O Orquestrador agora avisa visualmente de quem é a vez!
                    var nextPlayer = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
                    _battleLog.Log($"--- It is now {nextPlayer.Name}'s turn ---");

                    await _combatRepo.SaveAsync(combatId, gameState);

                    var logs = _battleLog.GetAndClearLogs();
                    await _notifier.SendBattleLogsAsync(combatId, logs);
                    await _notifier.SendGameStateUpdateAsync(combatId, gameState);

                    break;
                }

                if (_abilityRepo.TryGetDefinition(intent.AbilityId, out var abilityDef))
                {
                    var source = gameState.Combatants.First(c => c.Id == intent.SourceId);
                    var targets = new AbilityTargets { SelectedTargets = intent.TargetSelections };

                    _logger.LogInformation("AI using {Ability} from Source {SourceId}", abilityDef.Name, source.Id);

                    _engine.ExecuteAbility(gameState, abilityDef, source, targets, intent.Payment);

                    await _combatRepo.SaveAsync(combatId, gameState);

                    var logs = _battleLog.GetAndClearLogs();
                    await _notifier.SendBattleLogsAsync(combatId, logs);
                    await _notifier.SendGameStateUpdateAsync(combatId, gameState);
                }

                await Task.Delay(1500);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during AI turn execution.");
        }
    }
}