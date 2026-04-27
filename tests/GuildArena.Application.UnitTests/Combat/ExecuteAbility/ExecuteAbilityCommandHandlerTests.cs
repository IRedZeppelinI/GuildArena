using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results; 
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

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
            _combatRepoMock,
            _userMock,
            _abilityRepoMock,
            _engineMock,
            _battleLogMock,
            _notifierMock,
            _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldExecuteSuccessfully_WhenRequestIsValid()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 1;
        var sourceId = 10;
        var abilityId = "ABIL_FIRE";

        _userMock.UserId.Returns(userId);

        var combatant = new Combatant
        {
            Id = sourceId,
            OwnerId = userId,
            Name = "Mage",
            RaceId = "Human",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100,
            Abilities = new List<AbilityDefinition>
            {
                new AbilityDefinition { Id = abilityId, Name = "Fire" }
            }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = userId,
            Combatants = new List<Combatant> { combatant }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x =>
            {
                x[1] = new AbilityDefinition { Id = abilityId, Name = "Fire" };
                return true;
            });

        _engineMock.ExecuteAbility(
            gameState,
            Arg.Any<AbilityDefinition>(),
            combatant,
            Arg.Any<AbilityTargets>(),
            Arg.Any<Dictionary<EssenceType, int>>())
            .Returns(new List<CombatActionResult>
            {
                new CombatActionResult { IsSuccess = true }
            });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = sourceId,
            AbilityId = abilityId
        };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Validar que o Result é de Sucesso
        result.IsSuccess.ShouldBeTrue();

        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenResult_WhenNotUserTurn()
    {
        // ARRANGE
        var userId = 1;
        var enemyId = 2;
        var combatId = "C1";

        _userMock.UserId.Returns(userId);

        _combatRepoMock.GetAsync(combatId)
            .Returns(new GameState { CurrentPlayerId = enemyId });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 10,
            AbilityId = "A"
        };

        // ACT 
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Validamos que falhou e que o erro é exatamente o que esperamos
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotYourTurn");

        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenResult_WhenUserDoesNotOwnCombatant()
    {
        // ARRANGE
        var userId = 1;
        var enemyUserId = 2;
        var combatantId = 99;

        _userMock.UserId.Returns(userId);

        var enemyCombatant = new Combatant
        {
            Id = combatantId,
            OwnerId = enemyUserId,
            Name = "Enemy",
            RaceId = "X",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100
        };

        var gameState = new GameState
        {
            CurrentPlayerId = userId,
            Combatants = new List<Combatant> { enemyCombatant }
        };

        _combatRepoMock.GetAsync("C1").Returns(gameState);

        var command = new ExecuteAbilityCommand
        {
            CombatId = "C1",
            SourceId = combatantId,
            AbilityId = "A"
        };

        // ACT 
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotOwner");
    }

    [Fact]
    public async Task Handle_ShouldSendLogsAndReturnFailureResult_WhenEngineReturnsFailure()
    {
        // ARRANGE
        var userId = 1;
        var sourceId = 10;
        var abilityId = "ABIL_FAIL";
        var combatId = "C1";

        _userMock.UserId.Returns(userId);

        var combatant = new Combatant
        {
            Id = sourceId,
            OwnerId = userId,
            Name = "Hero",
            RaceId = "H",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100,
            Abilities = new List<AbilityDefinition> { new() { Id = abilityId, Name = "Fail Skill" } }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = userId,
            Combatants = new List<Combatant> { combatant }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => {
                x[1] = new AbilityDefinition { Id = abilityId, Name = "Fail" };
                return true;
            });

        _battleLogMock.GetAndClearLogs().Returns(new List<string> { "Not enough mana." });

        _engineMock.ExecuteAbility(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new List<CombatActionResult>
            {
                new CombatActionResult { IsSuccess = false }
            });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = sourceId,
            AbilityId = abilityId
        };

        // ACT 
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // O Handler devolve uma Falha de Domínio (Failure normal, não Forbidden ou NotFound)
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Failure);
        result.Error.Code.ShouldBe("Combat.ExecutionFailed");

        // Verificamos se o Notifier foi chamado para enviar o erro à UI
        await _notifierMock.Received(1).SendBattleLogsAsync(
            combatId,
            Arg.Is<List<string>>(logs => logs.Contains("Not enough mana.")));

        // Garantimos que NÃO guardou o estado (porque a ação falhou)
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);

        // Garantimos que NÃO enviou atualização de GameState (só enviou os logs do erro)
        await _notifierMock.DidNotReceiveWithAnyArgs().SendGameStateUpdateAsync(default!, default!);
    }
}