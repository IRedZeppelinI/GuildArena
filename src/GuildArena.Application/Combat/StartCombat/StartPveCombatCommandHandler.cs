using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.StartCombat;

public class StartPveCombatCommandHandler : IRequestHandler<StartPveCombatCommand, StartCombatResult>
{
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IEncounterDefinitionRepository _encounterRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICombatantFactory _combatantFactory;
    private readonly IEssenceService _essenceService;
    private readonly ILogger<StartPveCombatCommandHandler> _logger;
    private readonly IRandomProvider _rng;
    private readonly ITriggerProcessor _triggerProcessor;
    private readonly ICombatEngine _combatEngine;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBattleLogService _battleLog; 

    public StartPveCombatCommandHandler(
        ICombatStateRepository combatStateRepo,
        IPlayerRepository playerRepo,
        IEncounterDefinitionRepository encounterRepo,
        ICurrentUserService currentUser,
        ICombatantFactory combatantFactory,
        IEssenceService essenceService,
        ILogger<StartPveCombatCommandHandler> logger,
        IRandomProvider rng,
        ITriggerProcessor triggerProcessor,
        ICombatEngine combatEngine,
        IServiceScopeFactory scopeFactory,
        IBattleLogService battleLog) 
    {
        _combatStateRepo = combatStateRepo;
        _playerRepo = playerRepo;
        _encounterRepo = encounterRepo;
        _currentUser = currentUser;
        _combatantFactory = combatantFactory;
        _essenceService = essenceService;
        _logger = logger;
        _rng = rng;
        _triggerProcessor = triggerProcessor;
        _combatEngine = combatEngine;
        _scopeFactory = scopeFactory;
        _battleLog = battleLog; 
    }

    public async Task<StartCombatResult> Handle(StartPveCombatCommand request, CancellationToken cancellationToken)
    {
        var playerId = _currentUser.UserId;
        if (playerId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        if (request.HeroInstanceIds == null || !request.HeroInstanceIds.Any())
            throw new ArgumentException("You must select at least one hero to enter combat.");

        var playerTeamEntities = await _playerRepo.GetHeroesAsync(playerId.Value, request.HeroInstanceIds);

        if (playerTeamEntities.Count != request.HeroInstanceIds.Count)
            throw new InvalidOperationException("One or more selected heroes do not exist or do not belong to you.");

        if (!_encounterRepo.TryGetDefinition(request.EncounterId, out var encounterDef))
            throw new KeyNotFoundException($"Encounter '{request.EncounterId}' not found.");

        var combatId = Guid.NewGuid().ToString();
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            Combatants = new List<Combatant>(),
            Players = new List<CombatPlayer>()
        };

        var humanPlayer = new CombatPlayer
        {
            PlayerId = playerId.Value,
            Name = $"Player {playerId.Value}",
            Type = CombatPlayerType.Human,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(humanPlayer);

        int playerPosIndex = 0;
        foreach (var heroEntity in playerTeamEntities)
        {
            var combatant = _combatantFactory.Create(heroEntity, humanPlayer.PlayerId);
            combatant.Position = playerPosIndex++;
            gameState.Combatants.Add(combatant);
        }

        var aiPlayerId = 0;
        var aiPlayer = new CombatPlayer
        {
            PlayerId = aiPlayerId,
            Name = encounterDef.Name ?? "Enemy Encounter",
            Type = CombatPlayerType.AI,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(aiPlayer);

        int mobIdCounter = -1;
        foreach (var enemyDef in encounterDef.Enemies)
        {
            var mobEntity = new Hero
            {
                Id = mobIdCounter--,
                GuildId = -1,
                CharacterDefinitionId = enemyDef.CharacterDefinitionId,
                CurrentLevel = enemyDef.Level,
                CurrentXP = 0
            };

            var mobCombatant = _combatantFactory.Create(mobEntity, aiPlayerId);
            mobCombatant.Position = enemyDef.Position;
            gameState.Combatants.Add(mobCombatant);
        }

        // --- INICIALIZAÇÃO DE TURNO E LOGS ---
        _battleLog.Log($"--- Combat Started: {encounterDef.Name} ---");

        var startingPlayerId = _rng.Next(2) == 0 ? playerId.Value : aiPlayerId;
        gameState.CurrentPlayerId = startingPlayerId;

        var startingPlayer = gameState.Players.First(p => p.PlayerId == startingPlayerId);

        // Log explícito sobre quem começa 
        _battleLog.Log($"{startingPlayer.Name} won the coin toss and goes first!");

        _essenceService.GenerateStartOfTurnEssence(startingPlayer, baseAmount: 2);

        InitializeCombatTriggers(gameState);

        await _combatStateRepo.SaveAsync(combatId, gameState);

        if (startingPlayer.Type == CombatPlayerType.AI)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IAiTurnOrchestrator>();
                    await orchestrator.PlayTurnAsync(combatId, startingPlayerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background AI task failed for Combat {CombatId}", combatId);
                }
            });
        }

        // Extrai todos os logs acumulados e devolve no HTTP
        var initialLogs = _battleLog.GetAndClearLogs();

        return new StartCombatResult
        {
            CombatId = combatId,
            InitialLogs = initialLogs,
            InitialState = gameState
        };
    }

    private void InitializeCombatTriggers(GameState gameState)
    {
        foreach (var combatant in gameState.Combatants)
        {
            var context = new TriggerContext
            {
                Source = combatant,
                Target = combatant,
                GameState = gameState,
                Tags = new HashSet<string> { "StartCombat" }
            };

            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_COMBAT_START, context);
        }
        _combatEngine.ProcessPendingActions(gameState);
    }
}