using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Dungeons.EnterDungeonStage;

/// <summary>
/// Creates the combat session for a dungeon stage using the hero HP from the active run.
/// </summary>
public class EnterDungeonStageCommandHandler : IRequestHandler<EnterDungeonStageCommand, Result<StartCombatResult>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IDungeonRunRepository _dungeonRunRepo;
    private readonly IDungeonDefinitionRepository _dungeonDefRepo;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICombatantFactory _combatantFactory;
    private readonly IEssenceService _essenceService;
    private readonly ICombatEngine _combatEngine;
    private readonly IBattleLogService _battleLog;
    private readonly IAiTurnQueue _aiQueue;
    private readonly IRandomProvider _rng;
    private readonly ILogger<EnterDungeonStageCommandHandler> _logger;

    public EnterDungeonStageCommandHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IDungeonRunRepository dungeonRunRepo,
        IDungeonDefinitionRepository dungeonDefRepo,
        ICombatStateRepository combatStateRepo,
        ICombatantFactory combatantFactory,
        IEssenceService essenceService,
        ICombatEngine combatEngine,
        IBattleLogService battleLog,
        IAiTurnQueue aiQueue,
        IRandomProvider rng,
        ILogger<EnterDungeonStageCommandHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _dungeonRunRepo = dungeonRunRepo;
        _dungeonDefRepo = dungeonDefRepo;
        _combatStateRepo = combatStateRepo;
        _combatantFactory = combatantFactory;
        _essenceService = essenceService;
        _combatEngine = combatEngine;
        _battleLog = battleLog;
        _aiQueue = aiQueue;
        _rng = rng;
        _logger = logger;
    }

    public async Task<Result<StartCombatResult>> Handle(EnterDungeonStageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<StartCombatResult>(new Error("Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var guild = await _guildRepo.GetGuildByUserIdAsync(userId);
        if (guild == null)
            return Result.Failure<StartCombatResult>(new Error("Dungeon.NoGuild", "No guild found.", ErrorType.NotFound));

        var activeRun = await _dungeonRunRepo.GetActiveRunAsync(guild.Id, cancellationToken);
        if (activeRun == null)
            return Result.Failure<StartCombatResult>(new Error("Dungeon.NoActiveRun", "No active dungeon run.", ErrorType.NotFound));

        if (!_dungeonDefRepo.TryGetDefinition(activeRun.DungeonDefinitionId, out var dungeonDef))
            return Result.Failure<StartCombatResult>(new Error("Dungeon.DefinitionNotFound", "Dungeon definition missing.", ErrorType.NotFound));

        var stage = dungeonDef.Stages.SingleOrDefault(s => s.StageIndex == activeRun.CurrentStageIndex);
        if (stage == null)
            return Result.Failure<StartCombatResult>(new Error("Dungeon.InvalidState", "Current stage not found.", ErrorType.NotFound));

        // --- Build GameState (similar to encounter) ---
        var combatId = Guid.NewGuid().ToString();
        string bg = stage.BackgroundId ?? "bg_default";

        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            BackgroundId = bg,
            Combatants = new List<Combatant>(),
            Players = new List<CombatPlayer>(),
            Status = CombatStatus.Ongoing,
            MatchType = GuildArena.Domain.Enums.Matches.MatchType.Dungeon,
            ContextId = activeRun.DungeonDefinitionId
        };

        // Human player (seat 1)
        int humanSeat = 1;
        var humanPlayer = new CombatPlayer
        {
            PlayerId = humanSeat,
            UserId = userId,
            Name = guild.Name,
            Type = CombatPlayerType.Human,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(humanPlayer);

        // Create combatants from the saved heroes using HP override
        var heroesMap = await _guildRepo.GetAllHeroesAsync(guild.Id);
        int posIndex = 0;
        foreach (var heroState in activeRun.HeroesState)
        {
            var hero = heroesMap.FirstOrDefault(h => h.Id == heroState.HeroId);
            if (hero == null) continue;

            // hpOverride: the HP stored in the run (might be 0 for dead heroes)
            var combatant = _combatantFactory.Create(hero, humanSeat, hpOverride: heroState.CurrentHP);
            combatant.Position = posIndex++;
            gameState.Combatants.Add(combatant);
        }

        // AI player (seat -1)
        int aiSeat = -1;
        var aiPlayer = new CombatPlayer
        {
            PlayerId = aiSeat,
            UserId = null,
            Name = dungeonDef.Name + " (Enemies)",
            Type = CombatPlayerType.AI,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(aiPlayer);

        // Create mobs for the stage
        int mobIdCounter = -100;
        foreach (var enemy in stage.Enemies)
        {
            var mobEntity = new Hero
            {
                Id = mobIdCounter--,
                GuildId = -1,
                CharacterDefinitionId = enemy.CharacterDefinitionId,
                CurrentLevel = enemy.Level,
                CurrentXP = 0
            };
            var mob = _combatantFactory.Create(mobEntity, aiSeat);
            mob.Position = enemy.Position;
            gameState.Combatants.Add(mob);
        }

        // --- Initial logs, coin toss, essence, triggers (identical to encounter) ---
        _battleLog.Log($"--- Dungeon Stage {stage.StageIndex + 1} Started ---");
        int startingPlayerId = _rng.Next(2) == 0 ? humanSeat : aiSeat;
        gameState.CurrentPlayerId = startingPlayerId;

        var startingPlayer = gameState.Players.First(p => p.PlayerId == startingPlayerId);
        _battleLog.Log($"{startingPlayer.Name} goes first!");

        _essenceService.GenerateStartOfTurnEssence(startingPlayer);
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
            var context = new GuildArena.Core.Combat.ValueObjects.TriggerContext
            {
                Source = combatant,
                Target = combatant,
                GameState = gameState,
                Tags = new HashSet<string> { "StartCombat" }
            };
            // The trigger processor from the engine
            _combatEngine.TriggerProcessor.ProcessTriggers(
                GuildArena.Domain.Enums.Modifiers.ModifierTrigger.ON_COMBAT_START,
                context);
        }
    }
}