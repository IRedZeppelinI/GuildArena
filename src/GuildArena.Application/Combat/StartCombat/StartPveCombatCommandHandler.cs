using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.StartCombat;

/// <summary>
/// Initializes a PvE combat session by setting up the game state, applying initial triggers, 
/// and handling the first turn logic (including AI orchestration if applicable).
/// </summary>
public class StartPveCombatCommandHandler : IRequestHandler<StartPveCombatCommand, Result<StartCombatResult>>
{
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly IGuildRepository _guildRepo;
    private readonly IEncounterDefinitionRepository _encounterRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICombatantFactory _combatantFactory;
    private readonly IEssenceService _essenceService;
    private readonly ILogger<StartPveCombatCommandHandler> _logger;
    private readonly IRandomProvider _rng;
    private readonly ITriggerProcessor _triggerProcessor;
    private readonly ICombatEngine _combatEngine;
    private readonly IAiTurnQueue _aiQueue;
    private readonly IBattleLogService _battleLog;

    public StartPveCombatCommandHandler(
        ICombatStateRepository combatStateRepo,
        IGuildRepository guildRepo,
        IEncounterDefinitionRepository encounterRepo,
        ICurrentUserService currentUser,
        ICombatantFactory combatantFactory,
        IEssenceService essenceService,
        ILogger<StartPveCombatCommandHandler> logger,
        IRandomProvider rng,
        ITriggerProcessor triggerProcessor,
        ICombatEngine combatEngine,
        IAiTurnQueue aiQueue,
        IBattleLogService battleLog)
    {
        _combatStateRepo = combatStateRepo;
        _guildRepo = guildRepo;
        _encounterRepo = encounterRepo;
        _currentUser = currentUser;
        _combatantFactory = combatantFactory;
        _essenceService = essenceService;
        _logger = logger;
        _rng = rng;
        _triggerProcessor = triggerProcessor;
        _combatEngine = combatEngine;
        _aiQueue = aiQueue;
        _battleLog = battleLog;
    }

    /// <summary>
    /// Processes the start PvE combat command.
    /// </summary>
    /// <param name="request">The command containing the encounter details and selected heroes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the <see cref="StartCombatResult"/> on success. 
    /// Returns a failure result if validation fails (e.g., heroes not owned, encounter not found).
    /// </returns>
    public async Task<Result<StartCombatResult>> Handle(StartPveCombatCommand request, CancellationToken cancellationToken)
    {
        // 1. Validar utilizador autenticado (GUID)
        var accountId = _currentUser.UserId;
        if (string.IsNullOrEmpty(accountId))
        {
            return Result.Failure<StartCombatResult>(new Error("Auth.Unauthorized", "User is not authenticated.", ErrorType.Unauthorized));
        }

        if (request.HeroInstanceIds == null || !request.HeroInstanceIds.Any())
        {
            return Result.Failure<StartCombatResult>(new Error("Combat.NoHeroes", "You must select at least one hero.", ErrorType.Validation));
        }

        // 2. Usar o GuildRepository para buscar a Guilda
        var guild = await _guildRepo.GetGuildByUserIdAsync(accountId);
        if (guild == null)
        {
            return Result.Failure<StartCombatResult>(new Error("Combat.NoGuild", "You must create a guild first.", ErrorType.Forbidden));
        }

        // 3. Usar o MESMO GuildRepository para buscar os Heróis (validando Anti-Cheat)
        var playerTeamEntities = await _guildRepo.GetHeroesAsync(guild.Id, request.HeroInstanceIds);

        if (playerTeamEntities.Count != request.HeroInstanceIds.Count)
        {
            return Result.Failure<StartCombatResult>(new Error("Combat.InvalidHeroes", "One or more selected heroes do not exist or do not belong to you.", ErrorType.Forbidden));
        }

        if (!_encounterRepo.TryGetDefinition(request.EncounterId, out var encounterDef))
        {
            return Result.Failure<StartCombatResult>(new Error("Combat.EncounterNotFound", $"Encounter '{request.EncounterId}' not found.", ErrorType.NotFound));
        }

        var combatId = Guid.NewGuid().ToString();

        string selectedBg = encounterDef.BackgroundIds.Any()
            ? encounterDef.BackgroundIds[_rng.Next(encounterDef.BackgroundIds.Count)]
            : "bg_default";

        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            BackgroundId = selectedBg,
            Combatants = new List<Combatant>(),
            Players = new List<CombatPlayer>()
        };

        // HUMAN PLAYER (Cadeira 1)
        int humanMatchId = 1;
        var humanPlayer = new CombatPlayer
        {
            PlayerId = humanMatchId,
            UserId = accountId, // Vincula o acesso à conta para os outros handlers validarem
            Name = guild.Name,  // Privacidade mantida (usa o nome da guilda)
            Type = CombatPlayerType.Human,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };

        gameState.Players.Add(humanPlayer);

        int playerPosIndex = 0;
        foreach (var heroEntity in playerTeamEntities)
        {
            var combatant = _combatantFactory.Create(heroEntity, humanMatchId);
            combatant.Position = playerPosIndex++;
            gameState.Combatants.Add(combatant);
        }

        // AI PLAYER (Cadeira -1)
        int aiMatchId = -1;
        var aiPlayer = new CombatPlayer
        {
            PlayerId = aiMatchId,
            UserId = null,
            Name = encounterDef.Name ?? "Enemy Encounter",
            Type = CombatPlayerType.AI,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };

        gameState.Players.Add(aiPlayer);

        int mobIdCounter = -100;
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

            var mobCombatant = _combatantFactory.Create(mobEntity, aiMatchId);
            mobCombatant.Position = enemyDef.Position;
            gameState.Combatants.Add(mobCombatant);
        }

        _battleLog.Log($"--- Combat Started: {encounterDef.Name} ---");

        var startingPlayerId = _rng.Next(2) == 0 ? humanMatchId : aiMatchId;
        gameState.CurrentPlayerId = startingPlayerId;

        var startingPlayer = gameState.Players.First(p => p.PlayerId == startingPlayerId);

        _battleLog.Log($"{startingPlayer.Name} won the coin toss and goes first!");

        _essenceService.GenerateStartOfTurnEssence(startingPlayer, baseAmount: 2);

        InitializeCombatTriggers(gameState);

        _combatEngine.ProcessPendingActions(gameState);

        await _combatStateRepo.SaveAsync(combatId, gameState);

        if (startingPlayer.Type == CombatPlayerType.AI)
        {
            var aiRequest = new AiTurnRequest(combatId, startingPlayerId);
            await _aiQueue.EnqueueAsync(aiRequest, cancellationToken);
        }

        return new StartCombatResult
        {
            CombatId = combatId,
            InitialLogs = _battleLog.GetAndClearLogs(),
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
    }
}