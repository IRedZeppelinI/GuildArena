using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactoryMock;
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
        _scopeFactoryMock = Substitute.For<IServiceScopeFactory>();
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
            _scopeFactoryMock,
            _battleLogMock
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateCombat_AndReturnLogs_WhenPlayerStarts()
    {
        // ARRANGE
        var playerId = 123;
        var encounterId = "ENC_TEST";
        var heroIds = new List<int> { 10 };

        _currentUserMock.UserId.Returns(playerId);
        _playerRepoMock.GetHeroesAsync(playerId, heroIds).
            Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H1" } });

        var encounterDef = new EncounterDefinition
        {
            Id = encounterId,
            Name = "Bandit Ambush",
            Enemies = new List<EncounterDefinition.EncounterEnemy> { new() { CharacterDefinitionId = "M1", Position = 1 } }
        };
        _encounterRepoMock.TryGetDefinition(encounterId, out Arg.Any<EncounterDefinition>()).
            Returns(x => { x[1] = encounterDef; return true; });

        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>()).
            Returns(info => new Combatant 
            { 
                Id = info.Arg<Hero>().Id,
                Name = "Mock",
                RaceId = "R",
                BaseStats = new()
            });

        // RNG devolve 0 -> Humano ganha a moeda ao ar
        _rngMock.Next(2).Returns(0);

        // Simular alguns logs
        _battleLogMock.GetAndClearLogs().
            Returns(new List<string> { "Combat Started", "Player 123 won the coin toss" });

        var command = new StartPveCombatCommand { EncounterId = encounterId, HeroInstanceIds = heroIds };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.ShouldNotBeNull();
        Guid.TryParse(result.CombatId, out _).ShouldBeTrue();
        result.InitialLogs.Count.ShouldBe(2);

        await _combatStateRepoMock.Received(1).
            SaveAsync(result.CombatId, Arg.Is<GameState>(gs => gs.CurrentPlayerId == playerId));

        // Como o jogador humano começou, a AI NÃO deve ser disparada (não precisamos de testar explicitamente o task.run, 
        // mas testamos o caminho lógico através do state).
    }

    [Fact]
    public async Task Handle_ShouldTriggerAiOrchestrator_WhenEnemyWinsCoinToss()
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

        // RNG devolve 1 -> AI ganha a moeda ao ar!
        _rngMock.Next(2).Returns(1);

        // Preparamos o ScopeFactory para devolvermos um mock do orquestrador
        var serviceProviderMock = Substitute.For<IServiceProvider>();
        var orchestratorMock = Substitute.For<IAiTurnOrchestrator>();
        serviceProviderMock.GetService(typeof(IAiTurnOrchestrator)).Returns(orchestratorMock);

        var scopeMock = Substitute.For<IServiceScope>();
        scopeMock.ServiceProvider.Returns(serviceProviderMock);
        _scopeFactoryMock.CreateScope().Returns(scopeMock);

        var command = new StartPveCombatCommand { EncounterId = "E", HeroInstanceIds = new List<int> { 1 } };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Damos um pequeno delay no teste porque o Task.Run no handler é assíncrono e não esperado (Fire and Forget)
        await Task.Delay(100);

        // Verificamos se o orchestrator foi chamado para jogar!
        await orchestratorMock.Received(1).PlayTurnAsync(result.CombatId, 0); // 0 é o AI Player ID
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