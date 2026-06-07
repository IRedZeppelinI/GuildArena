using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Dungeons.EnterDungeonStage;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Dungeons.EnterDungeonStage;

public class EnterDungeonStageCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly IDungeonRunRepository _dungeonRunRepo = Substitute.For<IDungeonRunRepository>();
    private readonly IDungeonDefinitionRepository _dungeonDefRepo = Substitute.For<IDungeonDefinitionRepository>();
    private readonly ICombatStateRepository _combatStateRepo = Substitute.For<ICombatStateRepository>();
    private readonly ICombatantFactory _combatantFactory = Substitute.For<ICombatantFactory>();
    private readonly IEssenceService _essenceService = Substitute.For<IEssenceService>();
    private readonly ICombatEngine _combatEngine = Substitute.For<ICombatEngine>();
    private readonly IBattleLogService _battleLog = Substitute.For<IBattleLogService>();
    private readonly IAiTurnQueue _aiTurnQueue = Substitute.For<IAiTurnQueue>();
    private readonly IRandomProvider _rng = Substitute.For<IRandomProvider>();
    private readonly ILogger<EnterDungeonStageCommandHandler> _logger = Substitute.For<ILogger<EnterDungeonStageCommandHandler>>();

    private readonly EnterDungeonStageCommandHandler _handler;

    public EnterDungeonStageCommandHandlerTests()
    {
        _handler = new EnterDungeonStageCommandHandler(
            _currentUser, _guildRepo, _dungeonRunRepo, _dungeonDefRepo,
            _combatStateRepo, _combatantFactory, _essenceService, _combatEngine, _battleLog,
            _aiTurnQueue, _rng, _logger);
    }

    [Fact]
    public async Task Handle_NoAuth_ReturnsUnauthorized()
    {
        _currentUser.UserId.Returns((string?)null);
        var result = await _handler.Handle(new EnterDungeonStageCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NoGuild_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns((Guild?)null);
        var result = await _handler.Handle(new EnterDungeonStageCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NoActiveRun_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(new Guild { Id = 1, ApplicationUserId = "user1", Name = "G" });
        _dungeonRunRepo.GetActiveRunAsync(1, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);
        var result = await _handler.Handle(new EnterDungeonStageCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Success_CreatesCombatAndEnqueuesAIIfApplicable()
    {
        // Arrange
        var guild = new Guild { Id = 42, ApplicationUserId = "user1", Name = "Heroes inc" };
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);

        var stageDef = new Domain.Definitions.DungeonDefinition.DungeonStage
        {
            StageIndex = 0,
            BackgroundId = "bg_cave",
            Enemies = new List<Domain.Definitions.DungeonDefinition.DungeonEnemy>
            {
                new Domain.Definitions.DungeonDefinition.DungeonEnemy { CharacterDefinitionId = "MOB_GOBLIN", Level = 2, Position = 1 }
            }
        };
        var dungeonDef = new Domain.Definitions.DungeonDefinition
        {
            Id = "DUNGEON_1",
            Name = "Test Dungeon",
            Stages = new List<Domain.Definitions.DungeonDefinition.DungeonStage> { stageDef }
        };
        _dungeonDefRepo.TryGetDefinition("DUNGEON_1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!)
            .Returns(x => { x[1] = dungeonDef; return true; });

        var run = new ActiveDungeonRun
        {
            Id = 10,
            GuildId = 42,
            DungeonDefinitionId = "DUNGEON_1",
            CurrentStageIndex = 0,
            HeroesState = new List<DungeonHeroState>
            {
                new DungeonHeroState { HeroId = 101, CurrentHP = 50 },
                new DungeonHeroState { HeroId = 102, CurrentHP = 0 } // dead hero
            }
        };
        _dungeonRunRepo.GetActiveRunAsync(42, Arg.Any<CancellationToken>()).Returns(run);

        var heroes = new List<Hero>
        {
            new Hero { Id = 101, CharacterDefinitionId = "H1", CurrentLevel = 3 },
            new Hero { Id = 102, CharacterDefinitionId = "H2", CurrentLevel = 1 }
        };
        _guildRepo.GetAllHeroesAsync(42).Returns(heroes);

        // Factory returns dummy combatants
        var combatant1 = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Hero1",
            RaceId = "RACE_HUMAN",
            BaseStats = new Domain.ValueObjects.Stats.BaseStats(),
            Abilities = new(),
            ActiveModifiers = new()
        };
        var combatant2 = new Combatant
        {
            Id = 102,
            OwnerId = 1,
            Name = "Hero2",
            RaceId = "RACE_HUMAN",
            BaseStats = new Domain.ValueObjects.Stats.BaseStats(),
            Abilities = new(),
            ActiveModifiers = new()
        };
        var mob = new Combatant
        {
            Id = -100,
            OwnerId = -1,
            Name = "Goblin",
            RaceId = "RACE_HUMAN",
            BaseStats = new Domain.ValueObjects.Stats.BaseStats(),
            Abilities = new(),
            ActiveModifiers = new()
        };

        _combatantFactory.Create(heroes[0], 1, hpOverride: 50, loadoutModifierIds: null).Returns(combatant1);
        _combatantFactory.Create(heroes[1], 1, hpOverride: 0, loadoutModifierIds: null).Returns(combatant2);
        _combatantFactory.Create(Arg.Any<Hero>(), -1, hpOverride: null, loadoutModifierIds: null).Returns(mob);

        _rng.Next(2).Returns(0); // Human goes first

        // Act
        var result = await _handler.Handle(new EnterDungeonStageCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldNotBeNullOrWhiteSpace();

        // Check that the factory was called with correct HP overrides (síncrono, sem await)
        _combatantFactory.Received(1).Create(heroes[0], 1, hpOverride: 50, loadoutModifierIds: null);
        _combatantFactory.Received(1).Create(heroes[1], 1, hpOverride: 0, loadoutModifierIds: null);

        // Verify state saved
        await _combatStateRepo.Received(1).SaveAsync(Arg.Any<string>(), Arg.Any<GameState>());
        // AI queue should NOT be enqueued because human goes first
        await _aiTurnQueue.DidNotReceive().EnqueueAsync(Arg.Any<AiTurnRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AIStarts_EnqueuesAI()
    {
        // Similar setup but RNG returns 1 -> AI first
        var guild = new Guild { Id = 42, ApplicationUserId = "user1", Name = "Heroes inc" };
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);

        var stageDef = new Domain.Definitions.DungeonDefinition.DungeonStage
        {
            StageIndex = 0,
            Enemies = new List<Domain.Definitions.DungeonDefinition.DungeonEnemy>
            {
                new Domain.Definitions.DungeonDefinition.DungeonEnemy { CharacterDefinitionId = "MOB_GOBLIN", Level = 2, Position = 1 }
            }
        };
        var dungeonDef = new Domain.Definitions.DungeonDefinition { Id = "D1", Name = "D", Stages = new() { stageDef } };
        _dungeonDefRepo.TryGetDefinition("D1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!)
            .Returns(x => { x[1] = dungeonDef; return true; });

        var run = new ActiveDungeonRun
        {
            GuildId = 42,
            DungeonDefinitionId = "D1",
            CurrentStageIndex = 0,
            HeroesState = new List<DungeonHeroState>()
        };
        _dungeonRunRepo.GetActiveRunAsync(42, Arg.Any<CancellationToken>()).Returns(run);
        _guildRepo.GetAllHeroesAsync(42).Returns(new List<Hero>());

        var mob = new Combatant
        {
            Id = -100,
            OwnerId = -1,
            Name = "Goblin",
            RaceId = "RACE_HUMAN",
            BaseStats = new Domain.ValueObjects.Stats.BaseStats(),
            Abilities = new(),
            ActiveModifiers = new()
        };
        _combatantFactory.Create(Arg.Any<Hero>(), -1, hpOverride: null, loadoutModifierIds: null).Returns(mob);

        _rng.Next(2).Returns(1); // AI first

        var result = await _handler.Handle(new EnterDungeonStageCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldNotBeNullOrWhiteSpace();
        await _aiTurnQueue.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(r => r.CombatId == result.Value.CombatId && r.AiPlayerId == -1),
            Arg.Any<CancellationToken>());
    }
}