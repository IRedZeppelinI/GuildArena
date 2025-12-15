using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class TargetResolutionServiceTests
{
    private readonly ILogger<TargetResolutionService> _loggerMock;
    private readonly IRandomProvider _randomMock;
    private readonly TargetResolutionService _service;

    private readonly GameState _gameState;
    private readonly Combatant _source;

    public TargetResolutionServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<TargetResolutionService>>();
        _randomMock = Substitute.For<IRandomProvider>();

        // Configuração Padrão do Random Mock
        _randomMock.Next(Arg.Any<int>()).Returns(0);

        _service = new TargetResolutionService(_loggerMock, _randomMock);

        // Arrange Global: Cenário de Combate 2v2
        _source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Garret",
            RaceId = "RACE_HUMAN", 
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var ally = new Combatant
        {
            Id = 2,
            OwnerId = 1,
            Name = "Healer",
            RaceId = "RACE_AUREON", 
            CurrentHP = 50,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var enemy1 = new Combatant
        {
            Id = 3,
            OwnerId = 2,
            Name = "Kymera A",
            RaceId = "RACE_KYMERA", 
            CurrentHP = 20,
            MaxHP = 50,
            BaseStats = new BaseStats()
        };
        var enemy2 = new Combatant
        {
            Id = 4,
            OwnerId = 2,
            Name = "Kymera B",
            RaceId = "RACE_KYMERA",
            CurrentHP = 80,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };

        _gameState = new GameState
        {
            Combatants = new List<Combatant> { _source, ally, enemy1, enemy2 }
        };
    }

    // --- TESTES DE FILTRAGEM BÁSICA ---

    [Fact]
    public void ResolveTargets_Self_ShouldReturnOnlySource()
    {
        // Arrange
        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.Self };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(_source.Id);
    }

    [Fact]
    public void ResolveTargets_Enemy_ShouldFilterAlliesAndSelf()
    {
        // Arrange
        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 1,
            Strategy = TargetSelectionStrategy.Manual
        };

        // O jogador tenta selecionar um aliado (ID 2) e um inimigo (ID 3)
        var input = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 2, 3 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(3); // Só o Kymera A (Inimigo) é válido
    }

    // --- TESTES DE FILTRAGEM RACIAL  ---

    [Fact]
    public void ResolveTargets_WithRequiredRace_ShouldFilterNonMatching()
    {
        // Arrange
        // Regra: "Flux Poison" - Atinge Todos os Inimigos, mas SÓ se forem Kymera
        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.AllEnemies,
            RequiredRaceIds = new List<string> { "RACE_KYMERA" }
        };

        // Adicionar um inimigo Valdrin para testar o filtro (Valdrin não deve ser atingido)
        var valdrinEnemy = new Combatant
        {
            Id = 5,
            OwnerId = 2,
            Name = "Stone Wall",
            RaceId = "RACE_VALDRIN",
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100
        };
        _gameState.Combatants.Add(valdrinEnemy);

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        // Deve apanhar ID 3 e 4 (Kymeras), mas ignorar ID 5 (Valdrin)
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Id == 3);
        result.ShouldContain(c => c.Id == 4);
        result.ShouldNotContain(c => c.Id == 5);
    }

    [Fact]
    public void ResolveTargets_WithExcludedRace_ShouldFilterMatching()
    {
        // Arrange
        // Regra: "Mortal Support" - Cura todos os Aliados, mas Aureons (Divine) não precisam/aceitam
        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.AllAllies, // (Não inclui Self)
            ExcludedRaceIds = new List<string> { "RACE_AUREON" }
        };

        // O Ally (ID 2) é Aureon. Adicionado um Psylian extra como aliado.
        var psylianAlly = new Combatant
        {
            Id = 6,
            OwnerId = 1,
            Name = "Mind Weaver",
            RaceId = "RACE_PSYLIAN",
            BaseStats = new BaseStats(),
            CurrentHP = 10,
            MaxHP = 10
        };
        _gameState.Combatants.Add(psylianAlly);

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        // Deve apanhar ID 6 (Psylian), mas excluir ID 2 (Aureon)
        result.Count.ShouldBe(1);
        result.ShouldContain(c => c.Id == 6);
        result.ShouldNotContain(c => c.Id == 2);
    }

    // --- TESTES DE UNTARGETABLE ---

    [Fact]
    public void ResolveTargets_SingleTargetEnemy_WithUntargetable_ShouldBeIgnored()
    {
        // Arrange
        var enemy = _gameState.Combatants.First(c => c.Id == 3);

        enemy.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_GHOST",
            ActiveStatusEffects = new List<StatusEffectType> { StatusEffectType.Untargetable }
        });

        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.Enemy, Count = 1 };

        // Tenta selecionar manualmente o alvo intocável
        var input = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 3 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveTargets_AoE_WithUntargetable_ShouldHitAnyway()
    {
        // Arrange
        var enemy = _gameState.Combatants.First(c => c.Id == 3);

        enemy.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_GHOST",
            ActiveStatusEffects = new List<StatusEffectType> { StatusEffectType.Untargetable }
        });

        // Regra AoE (AllEnemies) -  AoE ignora Untargetable
        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.AllEnemies };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Id == 3);
    }

    // --- TESTES DE ESTRATÉGIA (HP & TIE-BREAK) ---

    [Fact]
    public void ResolveTargets_LowestHP_ShouldReturnWeakestEnemy()
    {
        // Arrange
        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 1,
            Strategy = TargetSelectionStrategy.LowestHP
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Single().Id.ShouldBe(3); // Kymera A (20 HP)
    }

    [Fact]
    public void ResolveTargets_LowestHP_WithTie_ShouldUseIdAsTieBreaker()
    {
        // Arrange
        var enemy1 = _gameState.Combatants.First(c => c.Id == 3);
        var enemy2 = _gameState.Combatants.First(c => c.Id == 4);
        enemy1.CurrentHP = 50;
        enemy2.CurrentHP = 50;

        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 1,
            Strategy = TargetSelectionStrategy.LowestHP
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Single().Id.ShouldBe(3); // 3 < 4
    }

    [Fact]
    public void ResolveTargets_LowestHPPercent_ShouldAccountForMaxHP()
    {
        // Arrange
        var tank = _gameState.Combatants.First(c => c.Id == 4); // Kymera B
        tank.MaxHP = 1000;
        tank.CurrentHP = 100; // 10%

        var squishy = _gameState.Combatants.First(c => c.Id == 3); // Kymera A
        squishy.MaxHP = 50;
        squishy.CurrentHP = 20; // 40%

        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 1,
            Strategy = TargetSelectionStrategy.LowestHPPercent
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Single().Id.ShouldBe(4); // 10% < 40%
    }

    // ---  RANDOM STRATEGY  ---
    [Fact]
    public void ResolveTargets_RandomStrategy_ShouldUseProvider()
    {
        // Arrange
        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 1,
            Strategy = TargetSelectionStrategy.Random
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Count.ShouldBe(1);
        _randomMock.Received(2).Next(Arg.Any<int>());
    }

    // --- TESTES DE VIVO/MORTO ---

    [Fact]
    public void ResolveTargets_DeadTargets_ShouldBeIgnoredByDefault()
    {
        // Arrange
        var enemy = _gameState.Combatants.First(c => c.Id == 3);
        enemy.CurrentHP = 0; // Morto

        var rule = new TargetingRule
        {
            RuleId = "T1",
            Type = TargetType.Enemy,
            Count = 2,
            Strategy = TargetSelectionStrategy.LowestHP
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Count.ShouldBe(1); // Só o vivo
        result.First().Id.ShouldBe(4);
    }

    [Fact]
    public void ResolveTargets_ReviveRule_ShouldOnlyTargetDead()
    {
        // Arrange
        var deadAlly = _gameState.Combatants.First(c => c.Id == 2);
        deadAlly.CurrentHP = 0; // Morto

        var rule = new TargetingRule
        {
            RuleId = "Revive",
            Type = TargetType.Ally,
            CanTargetDead = true,
            Count = 1
        };

        var input = new AbilityTargets { SelectedTargets = new() { { "Revive", new List<int> { 2 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.Single().Id.ShouldBe(2);
    }
}