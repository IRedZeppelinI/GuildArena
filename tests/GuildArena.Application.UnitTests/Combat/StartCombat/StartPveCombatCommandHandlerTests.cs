using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Application.Combat.StartCombat.StartEncounterCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.StartCombat;

public class StartPveCombatCommandHandlerTests
{
    private readonly ICombatStateRepository _combatStateRepoMock;
    private readonly IGuildRepository _guildRepoMock;
    private readonly IEncounterDefinitionRepository _encounterRepoMock;
    private readonly ICurrentUserService _currentUserMock;
    private readonly ICombatantFactory _factoryMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ILogger<StartEncounterCombatCommandHandler> _loggerMock;
    private readonly IRandomProvider _rngMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly ICombatEngine _combatEngineMock;
    private readonly IAiTurnQueue _aiQueueMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly StartEncounterCombatCommandHandler _handler;

    public StartPveCombatCommandHandlerTests()
    {
        _combatStateRepoMock = Substitute.For<ICombatStateRepository>();
        _guildRepoMock = Substitute.For<IGuildRepository>();
        _encounterRepoMock = Substitute.For<IEncounterDefinitionRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _factoryMock = Substitute.For<ICombatantFactory>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _loggerMock = Substitute.For<ILogger<StartEncounterCombatCommandHandler>>();
        _rngMock = Substitute.For<IRandomProvider>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _combatEngineMock = Substitute.For<ICombatEngine>();
        _aiQueueMock = Substitute.For<IAiTurnQueue>();
        _battleLogMock = Substitute.For<IBattleLogService>();

        _handler = new StartEncounterCombatCommandHandler(
            _combatStateRepoMock,
            _guildRepoMock,
            _encounterRepoMock,
            _currentUserMock,
            _factoryMock,
            _essenceServiceMock,
            _loggerMock,
            _rngMock,
            _triggerProcessorMock,
            _combatEngineMock,
            _aiQueueMock,
            _battleLogMock
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateCombat_WithRandomBackground_AndReturnLogs()
    {
        var accountId = "user-123";
        var guildId = 15;
        var encounterId = "ENC_TEST";
        var heroIds = new List<int> { 10 };

        _currentUserMock.UserId.Returns(accountId);

        _guildRepoMock.GetGuildByUserIdAsync(accountId).Returns(new Guild { Id = guildId, ApplicationUserId = accountId, Name = "My Guild" });

        // AQUI: O Mock do GuildRepository lida com os Heróis
        _guildRepoMock.GetHeroesAsync(guildId, heroIds).Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H1", GuildId = guildId } });

        var encounterDef = new EncounterDefinition
        {
            Id = encounterId,
            Name = "Bandit Ambush",
            BackgroundIds = new List<string> { "bg_forest_01", "bg_forest_02" },
            Enemies = new List<EncounterDefinition.EncounterEnemy> { new() { CharacterDefinitionId = "M1", Position = 1 } }
        };
        _encounterRepoMock.TryGetDefinition(encounterId, out Arg.Any<EncounterDefinition>()).Returns(x => { x[1] = encounterDef; return true; });

        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>()).Returns(info => new Combatant { Id = info.Arg<Hero>().Id, Name = "Mock", RaceId = "R", BaseStats = new() });

        _rngMock.Next(2).Returns(1, 0);

        _battleLogMock.GetAndClearLogs().Returns(new List<string> { "Combat Started" });

        var command = new StartEncounterCombatCommand { EncounterId = encounterId, HeroInstanceIds = heroIds };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var startResult = result.Value;

        Guid.TryParse(startResult.CombatId, out _).ShouldBeTrue();
        startResult.InitialState.ShouldNotBeNull();
        startResult.InitialState.Players.Count.ShouldBe(2);

        await _combatStateRepoMock.Received(1).SaveAsync(startResult.CombatId, Arg.Is<GameState>(gs =>
            gs.CurrentPlayerId == 1 &&
            gs.BackgroundId == "bg_forest_02" &&
            gs.Players.Any(p => p.UserId == accountId && p.PlayerId == 1)
        ));

        await _aiQueueMock.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
        // Valida que o ponteiro Redis foi criado para o jogador!
        await _combatStateRepoMock.Received(1).SetPlayerActiveCombatAsync(accountId, startResult.CombatId);
    }

    [Fact]
    public async Task Handle_ShouldEnqueueAiTurn_WhenEnemyWinsCoinToss()
    {
        var accountId = "user-123";
        var guildId = 15;
        var heroIds = new List<int> { 1 };

        _currentUserMock.UserId.Returns(accountId);
        _guildRepoMock.GetGuildByUserIdAsync(accountId).Returns(new Guild { Id = guildId, ApplicationUserId = accountId, Name = "My Guild" });
        _guildRepoMock.GetHeroesAsync(guildId, heroIds).Returns(new List<Hero> { new() { Id = 1, CharacterDefinitionId = "H", GuildId = guildId } });

        _encounterRepoMock.TryGetDefinition(Arg.Any<string>(), out Arg.Any<EncounterDefinition>())
            .Returns(x =>
            {
                x[1] = new EncounterDefinition { Id = "E", Name = "E", Enemies = new() };
                return true;
            });

        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>()).
            Returns(new Combatant { Id = 1, Name = "M", RaceId = "R", BaseStats = new() });

        _rngMock.Next(2).Returns(1);

        var command = new StartEncounterCombatCommand { EncounterId = "E", HeroInstanceIds = heroIds };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var startResult = result.Value;

        await _aiQueueMock.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(req => req.CombatId == startResult.CombatId && req.AiPlayerId == -1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        _currentUserMock.UserId.Returns((string?)null);
        var command = new StartEncounterCombatCommand { EncounterId = "ANY", HeroInstanceIds = new() { 1 } };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenNoHeroesSelected()
    {
        _currentUserMock.UserId.Returns("user-123");
        var command = new StartEncounterCombatCommand { EncounterId = "ENC", HeroInstanceIds = new List<int>() };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Combat.NoHeroes");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenPlayerHasNoGuild()
    {
        _currentUserMock.UserId.Returns("user-123");
        _guildRepoMock.GetGuildByUserIdAsync("user-123").Returns((Guild?)null);

        var command = new StartEncounterCombatCommand { EncounterId = "ENC", HeroInstanceIds = new List<int> { 10 } };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NoGuild");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenPlayerDoesNotOwnHero()
    {
        var accountId = "user-123";
        var guildId = 15;
        var requestedIds = new List<int> { 10, 999 };

        _currentUserMock.UserId.Returns(accountId);
        _guildRepoMock.GetGuildByUserIdAsync(accountId).Returns(new Guild { Id = guildId, ApplicationUserId = accountId, Name = "My Guild" });

        // Retorna só 1 herói
        _guildRepoMock.GetHeroesAsync(guildId, requestedIds).
            Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H", GuildId = guildId } });

        var command = new StartEncounterCombatCommand { EncounterId = "ENC", HeroInstanceIds = requestedIds };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.InvalidHeroes");
    }
}