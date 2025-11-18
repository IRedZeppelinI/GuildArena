using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class TurnManagerServiceTests
{
    private readonly ILogger<TurnManagerService> _loggerMock;
    private readonly ITurnManagerService _service;
    private GameState _gameState;

    public TurnManagerServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<TurnManagerService>>();
        _service = new TurnManagerService(_loggerMock);

        // Arrange Global: Criar um estado de combate 1v1 (Humano vs AI)
        var player1 = new CombatPlayer { PlayerId = 1, Type = CombatPlayerType.Human };
        var player2 = new CombatPlayer { PlayerId = 0, Type = CombatPlayerType.AI }; // AI tem ID 0

        var combatant1 = new Combatant { Id = 101, OwnerId = 1, Name = "Hero 1", BaseStats = new BaseStats(), CurrentHP = 30 };
        var combatant2 = new Combatant { Id = 102, OwnerId = 0, Name = "Mob 1", BaseStats = new BaseStats(), CurrentHP = 30 };

        _gameState = new GameState
        {
            Players = new List<CombatPlayer> { player1, player2 },
            Combatants = new List<Combatant> { combatant1, combatant2 },
            CurrentTurnNumber = 1,
            CurrentPlayerId = 1 // Começa o Jogador 1
        };
    }

    [Fact]
    public void AdvanceTurn_ShouldSwitchToNextPlayer()
    {
        // Act
        _service.AdvanceTurn(_gameState);

        // Assert
        _gameState.CurrentPlayerId.ShouldBe(0); // Deve passar do Jogador 1 para o Jogador 0 (AI)
        _gameState.CurrentTurnNumber.ShouldBe(1); // O número do turno ainda não incrementa
    }

    [Fact]
    public void AdvanceTurn_ShouldWrapAroundToFirstPlayer()
    {
        // Arrange
        _gameState.CurrentPlayerId = 0; // É a vez do AI

        // Act
        _service.AdvanceTurn(_gameState);

        // Assert
        _gameState.CurrentPlayerId.ShouldBe(1); // Deve voltar ao Jogador 1
    }

    [Fact]
    public void AdvanceTurn_ShouldIncrementTurnNumber_OnFullRound()
    {
        // Arrange
        _gameState.CurrentPlayerId = 0; // É a vez do último jogador (AI)
        _gameState.CurrentTurnNumber.ShouldBe(1);

        // Act
        _service.AdvanceTurn(_gameState);

        // Assert
        _gameState.CurrentPlayerId.ShouldBe(1);
        _gameState.CurrentTurnNumber.ShouldBe(2); // Deu a volta, incrementa o turno
    }

    [Fact]
    public void AdvanceTurn_ShouldTickCooldowns_ForEndingPlayer()
    {
        // Arrange
        var combatantFromPlayer1 = _gameState.Combatants.First(c => c.OwnerId == 1);
        combatantFromPlayer1.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "CD_Test", TurnsRemaining = 3 }); //

        var combatantFromPlayer0 = _gameState.Combatants.First(c => c.OwnerId == 0);
        combatantFromPlayer0.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "AI_CD", TurnsRemaining = 5 });

        // Act
        _service.AdvanceTurn(_gameState); // Jogador 1 termina o turno

        // Assert
        // O CD do Jogador 1 (que terminou) deve diminuir
        combatantFromPlayer1.ActiveCooldowns.First().TurnsRemaining.ShouldBe(2);

        // O CD do Jogador 0 (que vai começar) não deve mudar
        combatantFromPlayer0.ActiveCooldowns.First().TurnsRemaining.ShouldBe(5);
    }

    [Fact]
    public void AdvanceTurn_ShouldRemoveExpiredModifiers()
    {
        // Arrange
        var combatantFromPlayer1 = _gameState.Combatants.First(c => c.OwnerId == 1);
        combatantFromPlayer1.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_2_TURNOS", TurnsRemaining = 2 }); //
        combatantFromPlayer1.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_1_TURNO", TurnsRemaining = 1 });
        combatantFromPlayer1.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_PERMANENTE", TurnsRemaining = -1 });

        // Act
        _service.AdvanceTurn(_gameState); // Jogador 1 termina o turno

        // Assert
        combatantFromPlayer1.ActiveModifiers.Count.ShouldBe(2); // O de 1 turno foi removido
        combatantFromPlayer1.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_2_TURNOS" && m.TurnsRemaining == 1);
        combatantFromPlayer1.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_PERMANENTE" && m.TurnsRemaining == -1);
    }

    [Fact]
    public void AdvanceTurn_ShouldOnlyTickAliveCombatants_ForEndingPlayer()
    {
        // Arrange
        // O _gameState começa com o turno do Jogador 1

        // Combatente 1 (Vivo)
        var aliveCombatant = _gameState.Combatants.First(c => c.OwnerId == 1); // Este tem HP 30
        aliveCombatant.ActiveModifiers.Add(new ActiveModifier 
        { 
            DefinitionId = "MOD_ALIVE",
            TurnsRemaining = 3
        });

        // Combatente 2 (Morto) - do mesmo jogador
        var deadCombatant = new Combatant
        {
            Id = 103,
            OwnerId = 1, // Mesmo OwnerId
            Name = "Dead Hero",
            BaseStats = new BaseStats(),
            CurrentHP = 0 // IsAlive == false
        };
        //TODO: A adicionar mod a combatant morto. A rever
        deadCombatant.ActiveModifiers.Add(new ActiveModifier 
        { 
            DefinitionId = "MOD_DEAD",
            TurnsRemaining = 3 
        });
        _gameState.Combatants.Add(deadCombatant); // Adiciona o morto ao estado

        // Act
        _service.AdvanceTurn(_gameState); // O turno do Jogador 1 (que tem ambos) termina

        // Assert
        // O TurnManagerService só deve ter "tickado" o combatente vivo

        // O vivo diminuiu
        aliveCombatant.ActiveModifiers.First(m => m.DefinitionId == "MOD_ALIVE").TurnsRemaining.ShouldBe(2);

        // O morto foi ignorado e manteve-se igual
        deadCombatant.ActiveModifiers.First(m => m.DefinitionId == "MOD_DEAD").TurnsRemaining.ShouldBe(3);
    }
}