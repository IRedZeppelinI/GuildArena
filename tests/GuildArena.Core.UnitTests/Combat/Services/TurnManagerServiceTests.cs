using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class TurnManagerServiceTests
{
    private readonly ILogger<TurnManagerService> _loggerMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly ITurnManagerService _service;
    private readonly IBattleLogService _battleLogService;
    private GameState _gameState;

    public TurnManagerServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<TurnManagerService>>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _battleLogService = Substitute.For<IBattleLogService>();

        _service = new TurnManagerService(
            _loggerMock,
            _essenceServiceMock,
            _triggerProcessorMock,
            _battleLogService);

        var player1 = new CombatPlayer { PlayerId = 1, Type = CombatPlayerType.Human };
        var player2 = new CombatPlayer { PlayerId = 0, Type = CombatPlayerType.AI };

        var combatant1 = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Hero 1",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats(),
            CurrentHP = 30
        };

        var combatant2 = new Combatant
        {
            Id = 102,
            OwnerId = 0,
            Name = "Mob 1",
            RaceId = "RACE_MOB",
            BaseStats = new BaseStats(),
            CurrentHP = 30
        };

        _gameState = new GameState
        {
            Players = new List<CombatPlayer> { player1, player2 },
            Combatants = new List<Combatant> { combatant1, combatant2 },
            CurrentTurnNumber = 1,
            CurrentPlayerId = 1 // Começa o Jogador 1
        };
    }

    [Fact]
    public void AdvanceTurn_ShouldCallEssenceService_ForNewPlayer()
    {
        _service.AdvanceTurn(_gameState);

        _essenceServiceMock.Received(1).GenerateStartOfTurnEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 0),
            Arg.Any<int>()
        );
    }

    [Fact]
    public void AdvanceTurn_ShouldSwitchToNextPlayer()
    {
        _service.AdvanceTurn(_gameState);

        _gameState.CurrentPlayerId.ShouldBe(0);
        _gameState.CurrentTurnNumber.ShouldBe(1);
    }

    [Fact]
    public void AdvanceTurn_ShouldWrapAroundToFirstPlayer()
    {
        _gameState.CurrentPlayerId = 0; // É a vez do AI

        _service.AdvanceTurn(_gameState);

        _gameState.CurrentPlayerId.ShouldBe(1);
    }

    [Fact]
    public void AdvanceTurn_ShouldIncrementTurnNumber_OnFullRound()
    {
        _gameState.CurrentPlayerId = 0;
        _gameState.CurrentTurnNumber.ShouldBe(1);

        _service.AdvanceTurn(_gameState);

        _gameState.CurrentPlayerId.ShouldBe(1);
        _gameState.CurrentTurnNumber.ShouldBe(2);
    }

    [Fact]
    public void AdvanceTurn_ShouldTickCooldowns_ForEndingPlayer()
    {
        var combatantFromPlayer1 = _gameState.Combatants.First(c => c.OwnerId == 1);
        combatantFromPlayer1.ActiveCooldowns.Add(
            new ActiveCooldown { AbilityId = "CD_Test", TurnsRemaining = 3 });

        var combatantFromPlayer0 = _gameState.Combatants.First(c => c.OwnerId == 0);
        combatantFromPlayer0.ActiveCooldowns.Add
            (new ActiveCooldown { AbilityId = "AI_CD", TurnsRemaining = 5 });

        _service.AdvanceTurn(_gameState);

        combatantFromPlayer1.ActiveCooldowns.First().TurnsRemaining.ShouldBe(2);
        combatantFromPlayer0.ActiveCooldowns.First().TurnsRemaining.ShouldBe(5);
    }

    [Fact]
    public void AdvanceTurn_ShouldRemoveExpiredModifiers()
    {
        var combatantFromPlayer1 = _gameState.Combatants.First(c => c.OwnerId == 1);
        combatantFromPlayer1.ActiveModifiers.Add
            (new ActiveModifier { DefinitionId = "MOD_2_TURNOS", TurnsRemaining = 2 });
        combatantFromPlayer1.ActiveModifiers.Add(
            new ActiveModifier { DefinitionId = "MOD_1_TURNO", TurnsRemaining = 1 });
        combatantFromPlayer1.ActiveModifiers.Add(
            new ActiveModifier { DefinitionId = "MOD_PERMANENTE", TurnsRemaining = -1 });

        _service.AdvanceTurn(_gameState);

        combatantFromPlayer1.ActiveModifiers.Count.ShouldBe(2);
        combatantFromPlayer1.ActiveModifiers.ShouldContain(
            m => m.DefinitionId == "MOD_2_TURNOS" && m.TurnsRemaining == 1);
        combatantFromPlayer1.ActiveModifiers.ShouldContain(
            m => m.DefinitionId == "MOD_PERMANENTE" && m.TurnsRemaining == -1);
    }

    [Fact]
    public void AdvanceTurn_ShouldOnlyProcessAliveCombatants_ForEndingPlayer()
    {
        var aliveCombatant = _gameState.Combatants.First(c => c.OwnerId == 1);
        aliveCombatant.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_ALIVE",
            TurnsRemaining = 3
        });

        var deadCombatant = new Combatant
        {
            Id = 103,
            OwnerId = 1,
            Name = "Dead Hero",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats(),
            CurrentHP = 0
        };

        deadCombatant.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_DEAD",
            TurnsRemaining = 3
        });
        _gameState.Combatants.Add(deadCombatant);

        _service.AdvanceTurn(_gameState);

        aliveCombatant.ActiveModifiers.First(m => m.DefinitionId == "MOD_ALIVE").TurnsRemaining.ShouldBe(2);
        deadCombatant.ActiveModifiers.First(m => m.DefinitionId == "MOD_DEAD").TurnsRemaining.ShouldBe(3);

        _triggerProcessorMock.DidNotReceive().ProcessTriggers(
            ModifierTrigger.ON_TURN_END,
            Arg.Is<TriggerContext>(c => c.Source.Id == deadCombatant.Id)
        );
    }

    [Fact]
    public void AdvanceTurn_ShouldFireTriggers_ForStartAndEndTurn()
    {
        var endingCombatant = _gameState.Combatants.First(c => c.OwnerId == 1);
        var startingCombatant = _gameState.Combatants.First(c => c.OwnerId == 0);

        _service.AdvanceTurn(_gameState);

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_TURN_END,
            Arg.Is<TriggerContext>(ctx =>
                ctx.Source.Id == endingCombatant.Id &&
                ctx.Target!.Id == endingCombatant.Id &&
                ctx.Tags.Contains("TurnEnd")
            ));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_TURN_START,
            Arg.Is<TriggerContext>(ctx =>
                ctx.Source.Id == startingCombatant.Id &&
                ctx.Target!.Id == startingCombatant.Id &&
                ctx.Tags.Contains("TurnStart")
            ));
    }

    [Fact]
    public void AdvanceTurn_ShouldResetActionPoints_ForStartingPlayer()
    {
        var startingCombatant = _gameState.Combatants.First(c => c.OwnerId == 0);
        startingCombatant.ActionsTakenThisTurn = 5;

        var endingCombatant = _gameState.Combatants.First(c => c.OwnerId == 1);
        endingCombatant.ActionsTakenThisTurn = 2;

        _service.AdvanceTurn(_gameState);

        startingCombatant.ActionsTakenThisTurn.ShouldBe(0);
        endingCombatant.ActionsTakenThisTurn.ShouldBe(2);
    }
}