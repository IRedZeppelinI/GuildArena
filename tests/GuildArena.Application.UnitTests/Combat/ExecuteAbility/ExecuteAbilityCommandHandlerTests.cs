using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.ExecuteAbility;

public class ExecuteAbilityCommandHandlerTests
{
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly ICurrentUserService _userMock;
    private readonly IAbilityDefinitionRepository _abilityRepoMock;
    private readonly ICombatEngine _engineMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly ILogger<ExecuteAbilityCommandHandler> _loggerMock;
    private readonly ICombatNotifier _notifierMock;
    private readonly ExecuteAbilityCommandHandler _handler;

    public ExecuteAbilityCommandHandlerTests()
    {
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _userMock = Substitute.For<ICurrentUserService>();
        _abilityRepoMock = Substitute.For<IAbilityDefinitionRepository>();
        _engineMock = Substitute.For<ICombatEngine>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _loggerMock = Substitute.For<ILogger<ExecuteAbilityCommandHandler>>();
        _notifierMock = Substitute.For<ICombatNotifier>();

        _handler = new ExecuteAbilityCommandHandler(
            _combatRepoMock, _userMock, _abilityRepoMock, _engineMock,
            _battleLogMock, _notifierMock, _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldExecuteSuccessfully_WhenRequestIsValid()
    {
        var combatId = "C1";
        var requestUserId = "user-123"; 
        var seatId = 1;                 
        var sourceId = 10;
        var abilityId = "ABIL_FIRE";

        _userMock.UserId.Returns(requestUserId);

        var matchPlayer = new CombatPlayer { PlayerId = seatId, UserId = requestUserId };

        var combatant = new Combatant
        {
            Id = sourceId,
            OwnerId = seatId,
            Name = "Mage",
            RaceId = "Human",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100,
            Abilities = new List<AbilityDefinition> { new() { Id = abilityId, Name = "Fire" } }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = seatId,
            Players = new List<CombatPlayer> { matchPlayer },
            Combatants = new List<Combatant> { combatant }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = new AbilityDefinition { Id = abilityId, Name = "Fire" }; return true; });

        _engineMock.ExecuteAbility(Arg.Any<GameState>(), Arg.Any<AbilityDefinition>(), Arg.Any<Combatant>(), Arg.Any<AbilityTargets>(), Arg.Any<Dictionary<EssenceType, int>>())
            .Returns(new List<CombatActionResult> { new() { IsSuccess = true } });

        var command = new ExecuteAbilityCommand { CombatId = combatId, SourceId = sourceId, AbilityId = abilityId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenResult_WhenNotUserTurn()
    {
        var requestUserId = "user-123";
        var mySeatId = 1;
        var enemySeatId = 2; // Turno do inimigo

        _userMock.UserId.Returns(requestUserId);

        var matchPlayer = new CombatPlayer { PlayerId = mySeatId, UserId = requestUserId };

        _combatRepoMock.GetAsync("C1").Returns(new GameState
        {
            CurrentPlayerId = enemySeatId,
            Players = new List<CombatPlayer> { matchPlayer }
        });

        var command = new ExecuteAbilityCommand { CombatId = "C1", SourceId = 10, AbilityId = "A" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Combat.NotYourTurn");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenResult_WhenUserDoesNotOwnCombatant()
    {
        var requestUserId = "user-123";
        var mySeatId = 1;
        var enemySeatId = 2;
        var combatantId = 99;

        _userMock.UserId.Returns(requestUserId);

        var matchPlayer = new CombatPlayer { PlayerId = mySeatId, UserId = requestUserId };

        var enemyCombatant = new Combatant
        {
            Id = combatantId,
            OwnerId = enemySeatId, // Pertence ao inimigo
            Name = "Enemy",
            RaceId = "X",
            BaseStats = new BaseStats()
        };

        var gameState = new GameState
        {
            CurrentPlayerId = mySeatId,
            Players = new List<CombatPlayer> { matchPlayer },
            Combatants = new List<Combatant> { enemyCombatant }
        };

        _combatRepoMock.GetAsync("C1").Returns(gameState);

        var command = new ExecuteAbilityCommand { CombatId = "C1", SourceId = combatantId, AbilityId = "A" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Combat.NotOwner");
    }

    [Fact]
    public async Task Handle_ShouldSendLogsAndReturnFailureResult_WhenEngineReturnsFailure()
    {
        var requestUserId = "user-123";
        var seatId = 1;
        var combatId = "C1";
        var abilityId = "ABIL_FAIL";

        _userMock.UserId.Returns(requestUserId);

        var matchPlayer = new CombatPlayer { PlayerId = seatId, UserId = requestUserId };
        var combatant = new Combatant
        {
            Id = 10,
            OwnerId = seatId,
            Name = "Hero",
            RaceId = "H",
            BaseStats = new BaseStats(),
            Abilities = new List<AbilityDefinition> { new() { Id = abilityId, Name = "Fail Skill" } }
        };

        var gameState = new GameState { CurrentPlayerId = seatId, Players = new List<CombatPlayer> { matchPlayer }, Combatants = new List<Combatant> { combatant } };
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>()).Returns(x => { x[1] = new AbilityDefinition { Id = abilityId, Name = "Fail" }; return true; });
        _battleLogMock.GetAndClearLogs().Returns(new List<string> { "Not enough mana." });

        _engineMock.ExecuteAbility(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new List<CombatActionResult> { new() { IsSuccess = false } });

        var command = new ExecuteAbilityCommand { CombatId = combatId, SourceId = 10, AbilityId = abilityId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Combat.ExecutionFailed");

        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Is<List<string>>(logs => logs.Contains("Not enough mana.")));
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }
}