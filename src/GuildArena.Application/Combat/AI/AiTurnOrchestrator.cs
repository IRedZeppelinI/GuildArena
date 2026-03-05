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
            // Simula o tempo que um jogador humano demora a analisar o tabuleiro antes de jogar
            await Task.Delay(1500);

            while (true) // Loop do Turno (a IA joga até não ter mais ações)
            {
                var gameState = await _combatRepo.GetAsync(combatId);
                if (gameState == null || gameState.CurrentPlayerId != aiPlayerId)
                {
                    _logger.LogWarning("AI Turn cancelled. Combat missing or not AI's turn.");
                    break;
                }

                // 1. Pede ao Cérebro a próxima jogada
                var intent = _behavior.DecideNextAction(gameState, aiPlayerId);

                // 2. Se a IA devolveu NULL, significa que terminou o turno!
                if (intent == null)
                {
                    _logger.LogInformation("AI Player {Id} finished their turn.", aiPlayerId);

                    _turnManager.AdvanceTurn(gameState);
                    await _combatRepo.SaveAsync(combatId, gameState);

                    var logs = _battleLog.GetAndClearLogs();
                    await _notifier.SendBattleLogsAsync(combatId, logs);
                    await _notifier.SendGameStateUpdateAsync(combatId, gameState);

                    break; // Sai do Loop
                }

                // 3. A IA escolheu uma habilidade.
                if (_abilityRepo.TryGetDefinition(intent.AbilityId, out var abilityDef))
                {
                    var source = gameState.Combatants.First(c => c.Id == intent.SourceId);
                    var targets = new AbilityTargets { SelectedTargets = intent.TargetSelections };

                    _logger.LogInformation("AI using {Ability} from Source {SourceId}", abilityDef.Name, source.Id);

                    // Executa no Motor
                    _engine.ExecuteAbility(gameState, abilityDef, source, targets, intent.Payment);

                    // Guarda e Avisa o Cliente
                    await _combatRepo.SaveAsync(combatId, gameState);

                    var logs = _battleLog.GetAndClearLogs();
                    await _notifier.SendBattleLogsAsync(combatId, logs);
                    await _notifier.SendGameStateUpdateAsync(combatId, gameState);
                }

                // 4. Pausa dramática para o jogador ver a animação na UI antes da IA jogar a próxima carta
                await Task.Delay(1500);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during AI turn execution.");
            // Falha silenciosa para não deitar o servidor abaixo
        }
    }
}