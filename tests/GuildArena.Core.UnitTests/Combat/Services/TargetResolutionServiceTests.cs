using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
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
    private readonly TargetResolutionService _service;

    private readonly GameState _gameState;
    private readonly Combatant _source;

    public TargetResolutionServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<TargetResolutionService>>();
        _service = new TargetResolutionService(_loggerMock);

        // Arrange Global: Cenário de Combate 2v2
        _source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Hero",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var ally = new Combatant
        {
            Id = 2,
            OwnerId = 1,
            Name = "Ally",
            CurrentHP = 50,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var enemy1 = new Combatant
        {
            Id = 3,
            OwnerId = 2,
            Name = "Goblin A",
            CurrentHP = 20,
            MaxHP = 50,
            BaseStats = new BaseStats()
        };
        var enemy2 = new Combatant
        {
            Id = 4,
            OwnerId = 2,
            Name = "Goblin B",
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
        result.First().Id.ShouldBe(3); // Só o Goblin A (Inimigo) é válido
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

        // Regra AoE (AllEnemies) - Geralmente AoE ignora Untargetable (ex: Explosões)
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
        result.Single().Id.ShouldBe(3); // Goblin A (20 HP)
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
        var tank = _gameState.Combatants.First(c => c.Id == 4); // Goblin B
        tank.MaxHP = 1000;
        tank.CurrentHP = 100; // 10%

        var squishy = _gameState.Combatants.First(c => c.Id == 3); // Goblin A
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
            CanTargetDead = true, // Flag importante
            Count = 1
        };

        var input = new AbilityTargets { SelectedTargets = new() { { "Revive", new List<int> { 2 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.Single().Id.ShouldBe(2);
    }
}