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
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly TargetResolutionService _service;

    private readonly GameState _gameState;
    private readonly Combatant _source;

    public TargetResolutionServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<TargetResolutionService>>();
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _service = new TargetResolutionService(_loggerMock, _repoMock);

        // Arrange Global: Cenário de Combate 2v2
        // Player 1: Source (ID 1) + Ally (ID 2)
        // Player 2: Enemy 1 (ID 3) + Enemy 2 (ID 4)

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
        // O sistema deve ignorar o ID 2 porque a regra é "Enemy"
        var input = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 2, 3 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(3); // Só o Goblin A (Inimigo) é válido
    }

    // --- TESTES DE UNTARGETABLE ---

    [Fact]
    public void ResolveTargets_SingleTargetEnemy_WithStealth_ShouldBeIgnored()
    {
        // Arrange
        var stealthMod = new ModifierDefinition { 
            Id = "MOD_STEALTH",
            Name = "Hidden",
            IsUntargetable = true };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { "MOD_STEALTH", stealthMod } });

        var enemy = _gameState.Combatants.First(c => c.Id == 3);
        enemy.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_STEALTH" });

        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.Enemy, Count = 1 };
        // Tenta clicar no Stealth (UI deve impedir)
        var input = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 3 } } } }; 

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.ShouldBeEmpty(); 
    }

    [Fact]
    public void ResolveTargets_AoE_WithStealth_ShouldHitAnyway()
    {
        // Arrange
        var stealthMod = new ModifierDefinition { Id = "MOD_STEALTH", Name = "Hidden", IsUntargetable = true };
        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { "MOD_STEALTH", stealthMod } });

        var enemy = _gameState.Combatants.First(c => c.Id == 3);
        enemy.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_STEALTH" });

        // Regra AoE (AllEnemies)
        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.AllEnemies };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        // Deve apanhar os 2 inimigos, mesmo o que está em Stealth, 
        // porque AoE global não requer "Targeting" manual.
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Id == 3);
    }

    // --- TESTES DE ESTRATÉGIA (HP & TIE-BREAK) ---

    [Fact]
    public void ResolveTargets_LowestHP_ShouldReturnWeakestEnemy()
    {
        // Arrange
        // Inimigo 3 tem 20 HP, Inimigo 4 tem 80 HP
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
        // alterar o HP para provocar um empate
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
        // Deve ganhar o ID mais baixo (3 < 4) para garantir determinismo
        result.Single().Id.ShouldBe(3);
    }

    [Fact]
    public void ResolveTargets_LowestHPPercent_ShouldAccountForMaxHP()
    {
        // Arrange
        var tank = _gameState.Combatants.First(c => c.Id == 4); // Goblin B
        tank.MaxHP = 1000;
        tank.CurrentHP = 100; // 10% HP

        var squishy = _gameState.Combatants.First(c => c.Id == 3); // Goblin A
        squishy.MaxHP = 50;
        squishy.CurrentHP = 20; // 40% HP

        // Em valor absoluto, 20 < 100.
        // Mas em percentagem, 10% < 40%. O Tank está "mais ferido".

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
        result.Single().Id.ShouldBe(4); // Goblin B (10%)
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
            Count = 2, // Pede 2
            Strategy = TargetSelectionStrategy.LowestHP
        };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, new AbilityTargets());

        // Assert
        result.Count.ShouldBe(1); // Só devolve o vivo (ID 4)
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
            CanTargetDead = true, // <--- Regra de Reviver
            Count = 1
        };

        var input = new AbilityTargets { SelectedTargets = new() { { "Revive", new List<int> { 2 } } } };

        // Act
        var result = _service.ResolveTargets(rule, _source, _gameState, input);

        // Assert
        result.Single().Id.ShouldBe(2);
    }
}