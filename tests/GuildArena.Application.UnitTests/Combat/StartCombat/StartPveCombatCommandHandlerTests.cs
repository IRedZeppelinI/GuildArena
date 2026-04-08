using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Combat.StartCombat;

public class StartPveCombatCommandHandlerTests
{
    private readonly ICombatStateRepository _combatStateRepoMock;
    private readonly IPlayerRepository _playerRepoMock;
    private readonly IEncounterDefinitionRepository _encounterRepoMock;
    private readonly ICurrentUserService _currentUserMock;
    private readonly ICombatantFactory _factoryMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ILogger<StartPveCombatCommandHandler> _loggerMock;
    private readonly IRandomProvider _rngMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly ICombatEngine _combatEngineMock;
    private readonly IAiTurnQueue _aiQueueMock; 
    private readonly IBattleLogService _battleLogMock;
    private readonly StartPveCombatCommandHandler _handler;

    public StartPveCombatCommandHandlerTests()
    {
        _combatStateRepoMock = Substitute.For<ICombatStateRepository>();
        _playerRepoMock = Substitute.For<IPlayerRepository>();
        _encounterRepoMock = Substitute.For<IEncounterDefinitionRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _factoryMock = Substitute.For<ICombatantFactory>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _loggerMock = Substitute.For<ILogger<StartPveCombatCommandHandler>>();
        _rngMock = Substitute.For<IRandomProvider>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _combatEngineMock = Substitute.For<ICombatEngine>();
        _aiQueueMock = Substitute.For<IAiTurnQueue>(); 
        _battleLogMock = Substitute.For<IBattleLogService>();

        _handler = new StartPveCombatCommandHandler(
            _combatStateRepoMock,
            _playerRepoMock,
            _encounterRepoMock,
            _currentUserMock,
            _factoryMock,
            _essenceServiceMock,
            _loggerMock,
            _rngMock,
            _triggerProcessorMock,
            _combatEngineMock,
            _battleLogMock,            
            _aiQueueMock
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateCombat_WithRandomBackground_AndReturnLogs()
    {
        // ARRANGE
        var playerId = 123;
        var encounterId = "ENC_TEST";
        var heroIds = new List<int> { 10 };

        _currentUserMock.UserId.Returns(playerId);
        _playerRepoMock.GetHeroesAsync(playerId, heroIds).Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H1" } });

        var encounterDef = new EncounterDefinition
        {
            Id = encounterId,
            Name = "Bandit Ambush",
            BackgroundIds = new List<string> { "bg_forest_01", "bg_forest_02" },
            Enemies = new List<EncounterDefinition.EncounterEnemy> { new() { CharacterDefinitionId = "M1", Position = 1 } }
        };
        _encounterRepoMock.TryGetDefinition(encounterId, out Arg.Any<EncounterDefinition>())
            .Returns(x => { x[1] = encounterDef; return true; });

        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>()).Returns(info => new Combatant 
            { 
                Id = info.Arg<Hero>().Id,
                Name = "Mock",
                RaceId = "R",
                BaseStats = new()
            });

        // RNG: O primeiro Next(2) sorteia o Background. Devolve 1 ("bg_forest_02").
        // RNG: O segundo Next(2) atira a moeda ao ar. Devolve 0 (Player 1 ganha).
        _rngMock.Next(2).Returns(1, 0);

        _battleLogMock.GetAndClearLogs().Returns(new List<string> { "Combat Started" });

        var command = new StartPveCombatCommand { EncounterId = encounterId, HeroInstanceIds = heroIds };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.ShouldNotBeNull();
        Guid.TryParse(result.CombatId, out _).ShouldBeTrue();

        result.InitialState.ShouldNotBeNull();
        result.InitialState.Players.Count.ShouldBe(2);

        // Garantimos que o estado foi guardado com o cenário que o RNG sorteou (index 1)
        await _combatStateRepoMock.Received(1).
            SaveAsync(result.CombatId, Arg.Is<GameState>(gs =>
                gs.CurrentPlayerId == playerId &&
                gs.BackgroundId == "bg_forest_02"
            ));

        // A IA não deve ter sido enfileirada porque o Humano ganhou o Coin Toss
        await _aiQueueMock.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldEnqueueAiTurn_WhenEnemyWinsCoinToss()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns(1);
        _playerRepoMock.GetHeroesAsync(1, Arg.Any<List<int>>()).
            Returns(new List<Hero> { new() { Id = 1, CharacterDefinitionId = "H" } });

        _encounterRepoMock.TryGetDefinition(Arg.Any<string>(), out Arg.Any<EncounterDefinition>())
            .Returns(x =>
            {
                x[1] = new EncounterDefinition { Id = "E", Name = "E", Enemies = new() };
                return true;
            });

        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>()).
            Returns(new Combatant { Id = 1, Name = "M", RaceId = "R", BaseStats = new() });

        // RNG devolve 1 -> AI ganha a moeda ao ar e começa a jogar!
        _rngMock.Next(2).Returns(1);

        var command = new StartPveCombatCommand { EncounterId = "E", HeroInstanceIds = new List<int> { 1 } };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.ShouldNotBeNull();
        result.InitialState.ShouldNotBeNull();

        // Verificamos simplesmente se a Queue recebeu o bilhete, sem precisarmos de simular Scopes
        await _aiQueueMock.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(req => req.CombatId == result.CombatId && req.AiPlayerId == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        _currentUserMock.UserId.Returns((int?)null);
        var command = new StartPveCombatCommand { EncounterId = "ANY", HeroInstanceIds = new() { 1 } };

        await Should.ThrowAsync<UnauthorizedAccessException>(() => _handler.Handle
        (command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenNoHeroesSelected()
    {
        _currentUserMock.UserId.Returns(1);
        var command = new StartPveCombatCommand { EncounterId = "ENC", HeroInstanceIds = new List<int>() };

        await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenPlayerDoesNotOwnHero()
    {
        var playerId = 1;
        var requestedIds = new List<int> { 10, 999 };
        _currentUserMock.UserId.Returns(playerId);
        _playerRepoMock.GetHeroesAsync(playerId, requestedIds).
            Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H" } });
        var command = new StartPveCombatCommand { EncounterId = "ENC", HeroInstanceIds = requestedIds };

        await Should.ThrowAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }
}