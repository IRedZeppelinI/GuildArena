using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.Targeting;
using GuildArena.Domain.ValueObjects.Stats; 
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.UnitTests.Combat.ExecuteAbility;

public class ExecuteAbilityCommandHandlerTests
{
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly ICurrentUserService _userMock;
    private readonly IAbilityDefinitionRepository _abilityRepoMock;
    private readonly ICombatEngine _engineMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly ILogger<ExecuteAbilityCommandHandler> _loggerMock;
    private readonly ExecuteAbilityCommandHandler _handler;

    public ExecuteAbilityCommandHandlerTests()
    {
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _userMock = Substitute.For<ICurrentUserService>();
        _abilityRepoMock = Substitute.For<IAbilityDefinitionRepository>();
        _engineMock = Substitute.For<ICombatEngine>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _loggerMock = Substitute.For<ILogger<ExecuteAbilityCommandHandler>>();

        _handler = new ExecuteAbilityCommandHandler(
            _combatRepoMock,
            _userMock,
            _abilityRepoMock,
            _engineMock,
            _battleLogMock,
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

        // Setup do Utilizador
        _userMock.UserId.Returns(userId);

        // Setup do GameState
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
            CurrentPlayerId = userId, // É a vez do user
            Combatants = new List<Combatant> { combatant }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // Setup da Habilidade
        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x =>
            {
                x[1] = new AbilityDefinition { Id = abilityId, Name = "Fire" };
                return true;
            });

        // Setup do Engine (Sucesso)
        // Usamos Arg.Any para os parametros complexos para facilitar a leitura, 
        // já que o foco é o fluxo do handler e não o engine.
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
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Deve ter guardado o estado atualizado
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenNotUserTurn()
    {
        // ARRANGE
        var userId = 1;
        var enemyId = 2;
        var combatId = "C1";

        _userMock.UserId.Returns(userId);

        // Mock devolve estado onde é a vez do inimigo
        _combatRepoMock.GetAsync(combatId)
            .Returns(new GameState { CurrentPlayerId = enemyId });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 10,
            AbilityId = "A"
        };

        // ACT & ASSERT
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("not your turn");

        // Garante que nada foi salvo
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenUserDoesNotOwnCombatant()
    {
        // ARRANGE
        var userId = 1;
        var enemyUserId = 2;
        var combatantId = 99;

        _userMock.UserId.Returns(userId);

        // O combatente pertence ao INIMIGO
        var enemyCombatant = new Combatant
        {
            Id = combatantId,
            OwnerId = enemyUserId, // Dono diferente
            Name = "Enemy",
            RaceId = "X",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100
        };

        var gameState = new GameState
        {
            CurrentPlayerId = userId, // É a minha vez...
            Combatants = new List<Combatant> { enemyCombatant }
        };

        _combatRepoMock.GetAsync("C1").Returns(gameState);

        var command = new ExecuteAbilityCommand
        {
            CombatId = "C1",
            SourceId = combatantId,
            AbilityId = "A"
        };

        // ACT & ASSERT
        // Tentar controlar o boneco do inimigo deve falhar
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("do not own");
    }

    //[Fact]
    //public async Task Handle_ShouldThrowInvalidOperation_WhenEngineReturnsFailure()
    //{
    //    // ARRANGE
    //    var userId = 1;
    //    var sourceId = 10;
    //    var abilityId = "ABIL_FAIL";

    //    _userMock.UserId.Returns(userId);

    //    var combatant = new Combatant
    //    {
    //        Id = sourceId,
    //        OwnerId = userId,
    //        Name = "Hero",
    //        RaceId = "H",
    //        BaseStats = new BaseStats(),
    //        CurrentHP = 100,
    //        MaxHP = 100
    //    };

    //    var gameState = new GameState
    //    {
    //        CurrentPlayerId = userId,
    //        Combatants = new List<Combatant> { combatant }
    //    };

    //    _combatRepoMock.GetAsync("C1").Returns(gameState);

    //    _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
    //        .Returns(x =>
    //        {
    //            x[1] = new AbilityDefinition { Id = abilityId, Name = "Fail" };
    //            return true;
    //        });

    //    // O Engine diz que falhou (ex: não tem mana, está atordoado)
    //    _engineMock.ExecuteAbility(
    //        default!, default!, default!, default!, default!)
    //        .ReturnsForAnyArgs(new List<CombatActionResult>
    //        {
    //            new CombatActionResult { IsSuccess = false } // Falha lógica
    //        });

    //    var command = new ExecuteAbilityCommand
    //    {
    //        CombatId = "C1",
    //        SourceId = sourceId,
    //        AbilityId = abilityId
    //    };

    //    // ACT & ASSERT
    //    await Should.ThrowAsync<InvalidOperationException>(() =>
    //        _handler.Handle(command, CancellationToken.None));

    //    // Não deve guardar estado se a ação falhou logicamente
    //    await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    //}

    [Fact]
    public async Task Handle_ShouldReturnLogs_WhenEngineReturnsFailure()
    {
        // ARRANGE
        var userId = 1;
        var sourceId = 10;
        var abilityId = "ABIL_FAIL";

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

        _combatRepoMock.GetAsync("C1").Returns(gameState);

        _abilityRepoMock.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = new AbilityDefinition { Id = abilityId, Name = "Fail" };
                return true; });

        _battleLogMock.GetAndClearLogs().Returns(new List<string> { "Not enough mana." });

        // O Engine diz que falhou logicamente
        _engineMock.ExecuteAbility
            (default!,
            default!,
            default!,
            default!,
            default!)
            .ReturnsForAnyArgs(new List<CombatActionResult>
            {
                new CombatActionResult { IsSuccess = false }
            });

        var command = new ExecuteAbilityCommand
        {
            CombatId = "C1",
            SourceId = sourceId,
            AbilityId = abilityId
        };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.ShouldNotBeEmpty();
        result.First().ShouldBe("Not enough mana.");

        // Garantimos que NÃO guardou o estado (porque a ação falhou)
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }
}